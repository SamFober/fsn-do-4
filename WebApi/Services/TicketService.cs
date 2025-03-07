using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApi.Interfaces.Repositories;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Requests;
using WebApi.Models.Responses;
using WebApi.Exceptions;

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
                var order = await _repository.GetOrderByToken(orderToken, true);
                if (order == null || order.Status != OrderStatus.Pending)
                {
                    throw new OrderNotFoundException("Order not found or expired");
                }

                // Validate seats exist and are available
                if (!await _repository.AreSeatAvailable(order.PresentationId, request.SeatIds, order.OrderToken))
                {
                    throw new SeatNotAvailableException("Some selected seats are no longer available");
                }

                // Check for duplicates
                var existingSeatIds = order.Items.Select(i => i.SeatId).ToHashSet();
                if (request.SeatIds.Any(id => existingSeatIds.Contains(id)))
                {
                    throw new ArgumentException("One or more seats are already in your order");
                }

                // Lock new seats
                var seatLocks = request.SeatIds.Select(seatId => new SeatLock
                {
                    SeatId = seatId,
                    OrderToken = orderToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                }).ToList();

                if (!await _repository.AddSeatLocks(seatLocks))
                {
                    throw new SeatNotAvailableException("Failed to lock selected seats");
                }

                // Add new seats to order
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

                // After successfully locking the seats, update their availability
                await _repository.UpdateSeatAvailability(request.SeatIds, false);

                return new OrderResponse(order.OrderToken, order.Items.Select(i => i.SeatId).ToList());
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
                .OrderBy(s => s.RowNumber)
                .GroupBy(s => s.RowNumber);

            foreach (var row in seatsByRow)
            {
                var consecutiveSeats = FindConsecutiveInRow(row.ToList(), numberOfSeats);
                if (consecutiveSeats.Any())
                {
                    return consecutiveSeats.Select(s => s.Id).ToList();
                }
            }

            return new List<int>();
        }

        public async Task<List<int>> FindBestSplitSeats(int presentationId, int numberOfSeats)
        {
            var availableSeats = await _repository.GetAvailableSeats(presentationId, true);
            
            // Try to find seats in adjacent rows
            var seatsByRow = availableSeats
                .OrderBy(s => s.RowNumber)
                .GroupBy(s => s.RowNumber)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<Seat>();
            foreach (var row in seatsByRow.OrderBy(kvp => kvp.Key))
            {
                var seatsNeeded = numberOfSeats - result.Count;
                if (seatsNeeded <= 0) break;

                var seatsInRow = row.Value
                    .OrderBy(s => Math.Abs(s.SeatNumber - (row.Value.First().SeatNumber + row.Value.Last().SeatNumber) / 2))
                    .Take(seatsNeeded);

                result.AddRange(seatsInRow);
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