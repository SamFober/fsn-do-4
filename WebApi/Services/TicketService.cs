using Microsoft.EntityFrameworkCore;
using WebApi.Exceptions;
using WebApi.Interfaces.Repositories;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Requests;
using WebApi.Models.Responses;

namespace WebApi.Services
{
    public class TicketService : ITicketService
    {
        private readonly ITicketRepository _repository;
        private readonly ILogger<TicketService> _logger;
        private readonly ApplicationDbContext _context;

        public TicketService(
            ITicketRepository repository,
            ILogger<TicketService> logger,
            ApplicationDbContext context)
        {
            _repository = repository;
            _logger = logger;
            _context = context;
        }

        public async Task<OrderResponse> StartOrder(StartOrderRequest request)
        {
            try
            {
                var availableSeats = await _repository.GetAvailableSeats(request.PresentationId, true);
                if (!availableSeats.Any())
                {
                    throw new NoSeatsAvailableException("No seats available");
                }

                var orderToken = Guid.NewGuid();
                var selectedSeat = availableSeats.First();

                var seatLock = new SeatLock
                {
                    SeatId = selectedSeat.Id,
                    OrderToken = orderToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                };

                if (!await _repository.AddSeatLocks(new List<SeatLock> { seatLock }))
                {
                    throw new SeatNotAvailableException("Failed to lock selected seat");
                }

                var order = new TicketOrder
                {
                    OrderToken = orderToken,
                    PresentationId = request.PresentationId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Status = OrderStatus.Pending,
                    Items = new List<TicketOrderItem>
                    {
                        new()
                        {
                            SeatId = selectedSeat.Id,
                            CreatedAt = DateTime.UtcNow
                        }
                    }
                };

                if (!await _repository.SaveOrder(order))
                {
                    throw new Exception("Failed to save order");
                }

                // After successfully locking the seats, update their availability
                await _repository.UpdateSeatAvailability(new List<int> { selectedSeat.Id }, false);

                return new OrderResponse(order.OrderToken, new List<int> { selectedSeat.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting order");
                throw;
            }
        }

        public async Task<GroupOrderResponse> StartGroupOrder(StartGroupOrderRequest request)
        {
            try
            {
                if (request.NumberOfSeats <= 0 || request.NumberOfSeats > 20)
                {
                    throw new ArgumentException("Invalid number of seats requested");
                }

                var options = new Dictionary<string, SeatingOption>();
                var orderToken = Guid.NewGuid();

                // First try to find consecutive seats
                var consecutiveSeats = await FindConsecutiveSeats(request.PresentationId, request.NumberOfSeats);
                if (consecutiveSeats.Any())
                {
                    var consecutiveGroups = await GetSplitArrangement(consecutiveSeats);
                    options.Add("consecutive", new SeatingOption(
                        "Consecutive seats together",
                        consecutiveSeats,
                        DateTime.UtcNow.AddMinutes(10),
                        consecutiveGroups
                    ));
                }

                // Try consecutive seats with one less person if no exact match found
                if (request.NumberOfSeats > 1 && !consecutiveSeats.Any())
                {
                    var smallerConsecutive = await FindConsecutiveSeats(request.PresentationId, request.NumberOfSeats - 1);
                    if (smallerConsecutive.Any())
                    {
                        var smallerGroups = await GetSplitArrangement(smallerConsecutive);
                        options.Add("smaller_consecutive", new SeatingOption(
                            $"Consecutive seats for {request.NumberOfSeats - 1} people",
                            smallerConsecutive,
                            DateTime.UtcNow.AddMinutes(10),
                            smallerGroups
                        ));
                    }
                }

                // If no consecutive options, try split seating
                if (!options.ContainsKey("consecutive"))
                {
                    var splitSeats = await FindBestSplitSeats(request.PresentationId, request.NumberOfSeats);
                    if (splitSeats.Any())
                    {
                        var splitGroups = await GetSplitArrangement(splitSeats);
                        options.Add("split", new SeatingOption(
                            "Split seating arrangement",
                            splitSeats,
                            DateTime.UtcNow.AddMinutes(10),
                            splitGroups
                        ));
                    }
                }

                if (!options.Any())
                {
                    throw new NoSeatsAvailableException("No seats available");
                }

                // Create initial order without locking seats
                var order = new TicketOrder
                {
                    OrderToken = orderToken,
                    PresentationId = request.PresentationId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Status = OrderStatus.Pending,
                    AvailableOptions = options,
                    RequestedSeats = request.NumberOfSeats // Store the original requested number of seats
                };

                if (!await _repository.SaveOrder(order))
                {
                    throw new Exception("Failed to save order");
                }

                // After successfully locking the seats, update their availability
                await _repository.UpdateSeatAvailability(options.Values.SelectMany(o => o.SeatIds).ToList(), false);

                return new GroupOrderResponse
                {
                    OrderToken = orderToken,
                    HasConsecutiveSeats = options.ContainsKey("consecutive"),
                    AvailableOptions = options,
                    SeatIds = options.Values.FirstOrDefault()?.SeatIds ?? new List<int>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting group order");
                throw;
            }
        }

        public async Task<OrderResponse> SelectGroupSeatingOption(string option, StartGroupOptionRequest request)
        {
            try
            {
                var order = await _repository.GetOrderByToken(request.OrderToken);
                if (order == null || order.Status != OrderStatus.Pending)
                {
                    throw new OrderNotFoundException("Order not found or expired");
                }

                if (!order.AvailableOptions.TryGetValue(option, out var seatingOption))
                {
                    throw new ArgumentException("Invalid seating option");
                }

                // For smaller_consecutive option, we expect fewer seats than originally requested
                if (option == "smaller_consecutive")
                {
                    // This option is valid as long as it provides the advertised number of seats
                    if (seatingOption.SeatIds.Count != order.RequestedSeats - 1)
                    {
                        throw new ArgumentException($"Selected option provides {seatingOption.SeatIds.Count} seats, but expected {order.RequestedSeats - 1} seats");
                    }
                }
                else
                {
                    // For all other options, verify we get the full number of requested seats
                    if (seatingOption.SeatIds.Count != order.RequestedSeats)
                    {
                        throw new ArgumentException($"Selected option provides {seatingOption.SeatIds.Count} seats, but {order.RequestedSeats} were requested");
                    }
                }

                // Get all currently locked seats for this order
                var existingLocks = await _repository.GetSeatLocksByOrderToken(order.OrderToken);

                // Find seats that were locked but not selected in the chosen option
                var seatsToUnlock = existingLocks
                    .Where(l => !seatingOption.SeatIds.Contains(l.SeatId))
                    .ToList();

                // Remove locks for unselected seats
                if (seatsToUnlock.Any())
                {
                    await _repository.RemoveSeatLocks(seatsToUnlock);
                    // Make unselected seats available again
                    await _repository.UpdateSeatAvailability(seatsToUnlock.Select(l => l.SeatId).ToList(), true);
                }

                // Verify seats are still available (excluding our own remaining locks)
                if (!await _repository.AreSeatAvailable(order.PresentationId, seatingOption.SeatIds, order.OrderToken))
                {
                    throw new SeatNotAvailableException("Selected seats are no longer available");
                }

                // Update order with selected seats
                order.Items = seatingOption.SeatIds.Select(seatId => new TicketOrderItem
                {
                    SeatId = seatId,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                if (!await _repository.SaveOrder(order))
                {
                    throw new Exception("Failed to save order");
                }

                // After successfully updating the order, update seat availability for selected seats
                await _repository.UpdateSeatAvailability(seatingOption.SeatIds, false);

                return new OrderResponse(order.OrderToken, seatingOption.SeatIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting seating option");
                throw;
            }
        }

        public async Task<List<TicketResponse>> ConfirmOrder(Guid orderToken, ConfirmOrderRequest request)
        {
            var order = await _repository.GetOrderByToken(orderToken, true);
            if (order == null || order.Status != OrderStatus.Pending)
            {
                throw new OrderNotFoundException("Order not found or expired");
            }

            // Get the seat IDs from the order items
            var seatIds = order.Items.Select(i => i.SeatId).ToList();

            // Verify seats are still available (excluding our own locks)
            if (!await _repository.AreSeatAvailable(order.PresentationId, seatIds, orderToken))
            {
                throw new SeatNotAvailableException("Some selected seats are no longer available");
            }

            // Create tickets for the order
            var tickets = order.Items.Select(item => new Ticket
            {
                PresentationId = order.PresentationId,
                SeatId = item.SeatId,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                Status = TicketStatus.Reserved,
                PurchaseDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Presentation = order.Presentation!,
                Seat = item.Seat
            }).ToList();

            // Update order status
            order.Status = OrderStatus.Confirmed;
            await _repository.SaveOrder(order);

            // Create the tickets
            var createdTickets = await _repository.CreateTickets(tickets);

            return createdTickets.Select(t => new TicketResponse(
                t.Id,
                t.Presentation.Movie.Title,
                t.Presentation.Hall.Name,
                t.Presentation.StartTime,
                t.Presentation.EndTime,
                t.Seat.RowNumber,
                t.Seat.SeatNumber,
                t.CustomerName,
                t.CustomerEmail,
                t.Status,
                t.PurchaseDate
            )).ToList();
        }

        public async Task<OrderResponse> AddSeatsToOrder(Guid orderToken, AddSeatsRequest request)
        {
            try
            {
                var order = await _repository.GetOrderByToken(orderToken);

                if (order == null || order.Status != OrderStatus.Pending)
                {
                    throw new OrderNotFoundException("Order not found or expired");
                }

                // Verify seats are part of the presentation
                var presentation = await _repository.GetPresentationById(order.PresentationId);
                if (presentation == null)
                {
                    throw new Exception("Presentation not found");
                }

                // Verify seats are available
                if (!await _repository.AreSeatAvailable(order.PresentationId, request.SeatIds, orderToken))
                {
                    throw new SeatNotAvailableException("One or more seats are not available");
                }

                // Create locks for the seats using the order's existing expiration time
                var seatLocks = request.SeatIds.Select(seatId => new SeatLock
                {
                    SeatId = seatId,
                    OrderToken = orderToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = order.ExpiresAt // Use order's expiration time instead of a new one
                }).ToList();

                if (!await _repository.AddSeatLocks(seatLocks))
                {
                    throw new SeatNotAvailableException("Failed to lock selected seats");
                }

                // Add the seats to the order
                foreach (var seatId in request.SeatIds)
                {
                    order.Items.Add(new TicketOrderItem
                    {
                        SeatId = seatId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (!await _repository.SaveOrder(order))
                {
                    throw new Exception("Failed to save order");
                }

                await _repository.UpdateSeatAvailability(request.SeatIds, false);

                return new OrderResponse(orderToken, order.Items.Select(i => i.SeatId).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding seats to order");
                throw;
            }
        }

        public async Task<OrderResponse> RemoveSeatFromOrder(Guid orderToken, int seatId)
        {
            try
            {
                var order = await _repository.GetOrderByToken(orderToken, true);
                if (order == null || order.Status != OrderStatus.Pending)
                {
                    throw new OrderNotFoundException("Order not found or expired");
                }

                var itemToRemove = order.Items.FirstOrDefault(i => i.SeatId == seatId);
                if (itemToRemove == null)
                {
                    throw new ArgumentException("Seat not found in order");
                }

                // Remove seat from order
                order.Items.Remove(itemToRemove);

                // Get and remove seat locks through repository
                var locks = await _repository.GetExpiredLocks();
                var seatLocks = locks.Where(l => l.OrderToken == orderToken && l.SeatId == seatId).ToList();
                await _repository.RemoveSeatLocks(seatLocks);

                if (!await _repository.SaveOrder(order))
                {
                    throw new Exception("Failed to save order");
                }

                // After successfully removing the seat, update its availability
                await _repository.UpdateSeatAvailability(new List<int> { seatId }, true);

                return new OrderResponse(order.OrderToken, order.Items.Select(i => i.SeatId).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing seat from order");
                throw;
            }
        }

        public async Task<List<int>> FindConsecutiveSeats(int presentationId, int numberOfSeats)
        {
            var availableSeats = await _repository.GetAvailableSeats(presentationId, true);

            // Group seats by row
            var seatsByRow = availableSeats
                .GroupBy(s => s.RowNumber)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SeatNumber).ToList());

            // Find all possible consecutive seat groups
            var candidateGroups = new List<List<Seat>>();

            foreach (var row in seatsByRow)
            {
                var consecutiveSeats = FindConsecutiveInRow(row.Value, numberOfSeats);
                if (consecutiveSeats.Any())
                {
                    candidateGroups.Add(consecutiveSeats);
                }
            }

            if (!candidateGroups.Any())
            {
                return new List<int>();
            }

            // Get the presentation information asynchronously
            var presentation = await _context.Presentations
                .Include(p => p.Hall)
                .FirstOrDefaultAsync(p => p.Id == presentationId);

            if (presentation == null)
            {
                return candidateGroups.First().Select(s => s.Id).ToList(); // Just return the first group if we can't score
            }

            var hall = presentation.Hall;
            var middleRow = (int)Math.Ceiling(hall.Rows / 2.0);
            var middleSeat = (int)Math.Ceiling(hall.SeatsPerRow / 2.0);
            var idealRow = (int)Math.Ceiling(hall.Rows * 0.66); // 2/3 back is ideal

            // Calculate score for each candidate group (lower is better)
            var scoredGroups = candidateGroups.Select(group =>
            {
                // For consecutive seats, calculate center of the group
                var groupRowNumber = group.First().RowNumber;
                var groupStartSeat = group.Min(s => s.SeatNumber);
                var groupEndSeat = group.Max(s => s.SeatNumber);
                var groupCenterSeat = (groupStartSeat + groupEndSeat) / 2.0;

                // Row position score - prefer rows closer to ideal viewing distance
                var rowDistanceScore = Math.Abs(groupRowNumber - idealRow) * 3;

                // Center alignment score - prefer seats centered in the row
                var centerAlignmentScore = Math.Abs(groupCenterSeat - middleSeat) * 2;

                // Front row penalty
                var frontRowPenalty = (groupRowNumber <= 2) ? 10 : 0;

                // Side seats penalty
                var sideDistanceMin = Math.Min(groupStartSeat, hall.SeatsPerRow - groupEndSeat + 1);
                var sidePenalty = Math.Max(0, 3 - sideDistanceMin) * 5;

                var totalScore = rowDistanceScore + centerAlignmentScore + frontRowPenalty + sidePenalty;

                return new { Group = group, Score = totalScore };
            });

            // Select the group with the lowest score (best viewing experience)
            var bestGroup = scoredGroups
                .OrderBy(g => g.Score)
                .First()
                .Group;

            return bestGroup.Select(s => s.Id).ToList();
        }

        public async Task<List<int>> FindBestSplitSeats(int presentationId, int numberOfSeats)
        {
            // Get all available seats, already ordered by best viewing experience
            var availableSeats = await _repository.GetAvailableSeats(presentationId, true);

            // Early return if we don't have enough seats
            if (availableSeats.Count < numberOfSeats)
            {
                return new List<int>();
            }

            // Group by row to try to keep parties together where possible
            var seatsByRow = availableSeats
                .GroupBy(s => s.RowNumber)
                .OrderBy(g => g.First().RowNumber) // Order by row number
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SeatNumber).ToList());

            // Try to find consecutive seats in each row first
            var result = new List<Seat>();
            var seatsRemaining = numberOfSeats;

            // First, try to get consecutive groups in good rows
            foreach (var row in seatsByRow.OrderBy(r => Math.Abs(r.Key - availableSeats[0].RowNumber))) // Start with the best row
            {
                // If we already have enough seats, break
                if (seatsRemaining <= 0) break;

                // Find the largest consecutive group in this row
                for (int groupSize = Math.Min(seatsRemaining, row.Value.Count); groupSize > 1; groupSize--)
                {
                    var consecutive = FindConsecutiveInRow(row.Value, groupSize);
                    if (consecutive.Any())
                    {
                        result.AddRange(consecutive);
                        seatsRemaining -= consecutive.Count;

                        // Remove these seats from consideration
                        foreach (var seat in consecutive)
                        {
                            row.Value.Remove(seat);
                        }

                        // Try again with the remaining seats in this row
                        groupSize = Math.Min(seatsRemaining, row.Value.Count) + 1; // +1 to offset the loop decrement
                    }
                }
            }

            // If we still need more seats, add individual best seats
            if (seatsRemaining > 0)
            {
                // Filter out seats we've already selected
                var remainingSeats = availableSeats
                    .Where(s => !result.Any(selected => selected.Id == s.Id))
                    .Take(seatsRemaining)
                    .ToList();

                result.AddRange(remainingSeats);
            }

            return result.Select(s => s.Id).ToList();
        }

        private List<Seat> FindConsecutiveInRow(List<Seat> rowSeats, int count)
        {
            var orderedSeats = rowSeats.OrderBy(s => s.SeatNumber).ToList();

            for (int i = 0; i <= orderedSeats.Count - count; i++)
            {
                var consecutive = true;
                for (int j = 0; j < count - 1; j++)
                {
                    if (orderedSeats[i + j + 1].SeatNumber != orderedSeats[i + j].SeatNumber + 1)
                    {
                        consecutive = false;
                        break;
                    }
                }

                if (consecutive)
                {
                    return orderedSeats.Skip(i).Take(count).ToList();
                }
            }

            return new List<Seat>();
        }

        private async Task<List<RowGroup>> GetSplitArrangement(List<int> seatIds)
        {
            var seats = await _repository.GetSeatsByIds(seatIds);

            return seats
                .GroupBy(s => s.RowNumber)
                .OrderBy(g => g.Key)
                .Select(g => new RowGroup(
                    g.Key,
                    g.Select(s => s.Id).ToList(),
                    $"{g.Count()} seat{(g.Count() > 1 ? "s" : "")} in row {g.Key}"
                ))
                .ToList();
        }

        /// <summary>
        /// Cancels a pending order and releases any locked seats
        /// </summary>
        /// <param name="orderToken">The unique identifier for the order</param>
        /// <exception cref="OrderNotFoundException">Thrown when the order is not found</exception>
        public async Task CancelOrder(Guid orderToken)
        {
            try
            {
                await _repository.CancelOrder(orderToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderToken}", orderToken);
                throw;
            }
        }
    }
}