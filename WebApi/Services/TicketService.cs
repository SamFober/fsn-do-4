using Microsoft.EntityFrameworkCore;
using MimeKit;
using WebApi.Exceptions;
using WebApi.Interfaces.Repositories;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Exceptions;
using WebApi.Models.Requests;
using WebApi.Models.Responses;

namespace WebApi.Services
{
    public class TicketService : ITicketService
    {
        private readonly ITicketRepository _repository;
        private readonly IMailService _mailService;
        private readonly ITicketPdfService _ticketPdfService;
        private readonly ILogger<TicketService> _logger;
        private readonly ApplicationDbContext _context;

        public TicketService(
            ITicketRepository repository,
            IMailService mailService,
            ITicketPdfService ticketPdfService,
            ILogger<TicketService> logger,
            ApplicationDbContext context)
        {
            _repository = repository;
            _mailService = mailService;
            _ticketPdfService = ticketPdfService;
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

                // First, create and save the order
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

                // Then create the seat lock
                var seatLock = new SeatLock
                {
                    SeatId = selectedSeat.Id,
                    PresentationId = request.PresentationId,
                    OrderToken = orderToken,
                    TicketOrderId = order.Id,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                };

                if (!await _repository.AddSeatLocks(new List<SeatLock> { seatLock }))
                {
                    throw new SeatNotAvailableException("Failed to lock selected seat");
                }

                // After successfully locking the seats, update their availability
                await _repository.UpdateSeatAvailability(new List<int> { selectedSeat.Id }, false, request.PresentationId);

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
                _logger.LogInformation("Starting group order for presentation {PresentationId} with {NumberOfSeats} seats", 
                    request.PresentationId, request.NumberOfSeats);
                
                if (request.NumberOfSeats <= 0 || request.NumberOfSeats > 20)
                {
                    _logger.LogWarning("Invalid number of seats requested: {NumberOfSeats}", request.NumberOfSeats);
                    throw new ArgumentException("Number of seats must be between 1 and 20");
                }

                // Get presentation
                var presentation = await _repository.GetPresentationById(request.PresentationId);
                if (presentation == null)
                {
                    _logger.LogWarning("Presentation {PresentationId} not found", request.PresentationId);
                    throw new ArgumentException($"Presentation {request.PresentationId} not found");
                }

                // Initialize seat presentations if they don't exist yet
                bool hasExistingRecords = await _context.SeatPresentations
                    .AnyAsync(sp => sp.PresentationId == request.PresentationId);
                    
                if (!hasExistingRecords)
                {
                    _logger.LogWarning("No seat presentations found for presentation {PresentationId}, initializing them now", request.PresentationId);
                    await _repository.InitializeSeatPresentations(request.PresentationId);
                }

                // Try to find consecutive seats first
                var consecutiveSeats = await FindConsecutiveSeats(request.PresentationId, request.NumberOfSeats);
                
                _logger.LogInformation("Found {SeatCount} consecutive seats for presentation {PresentationId}", 
                    consecutiveSeats.Count, request.PresentationId);

                // Options to offer to the user
                var options = new Dictionary<string, SeatingOption>();

                // If we have enough consecutive seats
                if (consecutiveSeats.Count >= request.NumberOfSeats)
                {
                    // Select best consecutive seats
                    var bestConsecutiveSeats = consecutiveSeats.Take(request.NumberOfSeats).ToList();
                    
                    // Get row grouping information
                    var rows = await _repository.GetSeatsByIds(bestConsecutiveSeats);
                    var rowGroups = rows
                        .GroupBy(s => s.RowNumber)
                        .Select(g => new RowGroup(
                            g.Key, 
                            g.Select(s => s.Id).ToList(),
                            $"{g.Count()} seats in row {g.Key}" 
                        ))
                        .ToList();
                        
                    var option = new SeatingOption(
                        "Consecutive seats together",
                        bestConsecutiveSeats,
                        DateTime.UtcNow.AddMinutes(10),
                        rowGroups
                    );
                    
                    options["consecutive"] = option;
                }
                else if (consecutiveSeats.Count >= request.NumberOfSeats - 1)
                {
                    // Offer smaller consecutive group (one less seat)
                    var smallerConsecutiveSeats = consecutiveSeats.Take(request.NumberOfSeats - 1).ToList();
                    
                    // Get row grouping information
                    var rows = await _repository.GetSeatsByIds(smallerConsecutiveSeats);
                    var rowGroups = rows
                        .GroupBy(s => s.RowNumber)
                        .Select(g => new RowGroup(
                            g.Key, 
                            g.Select(s => s.Id).ToList(),
                            $"{g.Count()} seats in row {g.Key}" 
                        ))
                        .ToList();
                        
                    var option = new SeatingOption(
                        $"{request.NumberOfSeats - 1} consecutive seats together",
                        smallerConsecutiveSeats,
                        DateTime.UtcNow.AddMinutes(10),
                        rowGroups
                    );
                    
                    options["smaller_consecutive"] = option;
                }

                // If we don't have enough consecutive seats, try split seating
                if (options.Count == 0)
                {
                    var splitSeats = await FindBestSplitSeats(request.PresentationId, request.NumberOfSeats);
                    
                    if (splitSeats.Count < request.NumberOfSeats)
                    {
                        _logger.LogWarning("Not enough seats available for presentation {PresentationId} - requested {RequestedSeats}, found {FoundSeats}", 
                            request.PresentationId, request.NumberOfSeats, splitSeats.Count);
                        throw new NoSeatsAvailableException("Not enough available seats");
                    }
                    
                    // Get row grouping information
                    var rows = await _repository.GetSeatsByIds(splitSeats);
                    var rowGroups = rows
                        .GroupBy(s => s.RowNumber)
                        .Select(g => new RowGroup(
                            g.Key, 
                            g.Select(s => s.Id).ToList(),
                            $"{g.Count()} seats in row {g.Key}" 
                        ))
                        .OrderBy(g => g.RowNumber)
                        .ToList();
                        
                    var option = new SeatingOption(
                        "Best available seats (split across rows)",
                        splitSeats,
                        DateTime.UtcNow.AddMinutes(10),
                        rowGroups
                    );
                    
                    options["split"] = option;
                }

                if (options.Count == 0)
                {
                    _logger.LogWarning("No seating options found for presentation {PresentationId} with {NumberOfSeats} seats", 
                        request.PresentationId, request.NumberOfSeats);
                    throw new NoSeatsAvailableException("No available seating options");
                }

                // Create an order (initially without locking seats)
                var orderToken = Guid.NewGuid();
                var order = new TicketOrder
                {
                    OrderToken = orderToken,
                    PresentationId = request.PresentationId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Status = OrderStatus.Pending,
                    RequestedSeats = request.NumberOfSeats,
                    AvailableOptions = options
                };
                
                _logger.LogInformation("Creating initial order {OrderToken} for presentation {PresentationId} with options: {Options}", 
                    orderToken, request.PresentationId, string.Join(", ", options.Keys));

                // Save the order
                var savedOrder = await _repository.SaveOrder(order);
                if (!savedOrder)
                {
                    _logger.LogError("Failed to save order {OrderToken}", orderToken);
                    throw new Exception("Failed to save order");
                }

                // Get the seat IDs from the first available option
                var initialSeatIds = options.Values.FirstOrDefault()?.SeatIds ?? new List<int>();
                
                if (initialSeatIds.Any())
                {
                    // Create locks for the selected seats
                    var locks = initialSeatIds.Select(seatId => new SeatLock
                    {
                        SeatId = seatId,
                        OrderToken = orderToken,
                        PresentationId = request.PresentationId, // Make sure this matches the order's presentation ID
                        TicketOrderId = order.Id,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                    }).ToList();
                    
                    var addedLocks = await _repository.AddSeatLocks(locks);
                    
                    _logger.LogInformation("Created {LockCount} locks for order {OrderToken} presentation {PresentationId}", 
                        locks.Count, orderToken, request.PresentationId);
                    
                    if (!addedLocks)
                    {
                        _logger.LogError("Failed to create locks for order {OrderToken}", orderToken);
                    }
                    
                    // Update seat availability for the initially locked seats
                    _logger.LogInformation("Marking {SeatCount} initial seats as unavailable for presentation {PresentationId}", 
                        initialSeatIds.Count, request.PresentationId);
                    await _repository.UpdateSeatAvailability(initialSeatIds, false, request.PresentationId);
                }
                
                // Construct the response with the proper constructor that sets HasConsecutiveSeats
                var response = new GroupOrderResponse(
                    orderToken, 
                    initialSeatIds,
                    options.ContainsKey("consecutive")
                )
                {
                    AvailableOptions = options
                };

                _logger.LogInformation("Returning group order response with token {OrderToken} and {OptionCount} options", 
                    orderToken, options.Count);
                return response;
            }
            catch (NoSeatsAvailableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting group order for presentation {PresentationId}, seats: {NumberOfSeats}: {ErrorMessage}",
                    request.PresentationId, request.NumberOfSeats, ex.Message);
                throw;
            }
        }

        public async Task<OrderResponse> SelectGroupSeatingOption(string option, StartGroupOptionRequest request)
        {
            try
            {
                _logger.LogInformation("Selecting option {Option} for order {OrderToken}", option, request.OrderToken);
                
                var order = await _repository.GetOrderByToken(request.OrderToken);
                if (order == null || order.Status != OrderStatus.Pending)
                {
                    _logger.LogWarning("Order {OrderToken} not found or expired", request.OrderToken);
                    throw new OrderNotFoundException("Order not found or expired");
                }

                _logger.LogInformation("Found order {OrderToken} for presentation {PresentationId}", 
                    request.OrderToken, order.PresentationId);

                if (!order.AvailableOptions.TryGetValue(option, out var seatingOption))
                {
                    _logger.LogWarning("Invalid seating option {Option} for order {OrderToken}", option, request.OrderToken);
                    throw new ArgumentException("Invalid seating option");
                }

                // For smaller_consecutive option, we expect fewer seats than originally requested
                if (option == "smaller_consecutive")
                {
                    // This option is valid as long as it provides the advertised number of seats
                    if (seatingOption.SeatIds.Count != order.RequestedSeats - 1)
                    {
                        _logger.LogWarning("Selected option {Option} provides {SeatCount} seats, but expected {ExpectedSeatCount} seats", 
                            option, seatingOption.SeatIds.Count, order.RequestedSeats - 1);
                        throw new ArgumentException($"Selected option provides {seatingOption.SeatIds.Count} seats, but expected {order.RequestedSeats - 1} seats");
                    }
                }
                else
                {
                    // For all other options, verify we get the full number of requested seats
                    if (seatingOption.SeatIds.Count != order.RequestedSeats)
                    {
                        _logger.LogWarning("Selected option {Option} provides {SeatCount} seats, but {RequestedSeatCount} were requested", 
                            option, seatingOption.SeatIds.Count, order.RequestedSeats);
                        throw new ArgumentException($"Selected option provides {seatingOption.SeatIds.Count} seats, but {order.RequestedSeats} were requested");
                    }
                }

                // Get existing locks for this order
                _logger.LogInformation("Retrieving existing locks for order {OrderToken}", request.OrderToken);
                var existingLocks = await _repository.GetLocksByOrder(request.OrderToken);
                _logger.LogInformation("Found {Count} existing locks for order", existingLocks.Count);

                // Find seats that were locked but not selected in the chosen option
                var seatsToUnlock = existingLocks
                    .Where(l => !seatingOption.SeatIds.Contains(l.SeatId))
                    .ToList();

                _logger.LogInformation("Unlocking {UnlockCount} unselected seats for order {OrderToken}: {SeatIds}", 
                    seatsToUnlock.Count, request.OrderToken, 
                    seatsToUnlock.Any() ? string.Join(", ", seatsToUnlock.Select(l => l.SeatId)) : "none");

                // Remove locks for unselected seats
                if (seatsToUnlock.Any())
                {
                    await _repository.RemoveSeatLocks(seatsToUnlock);
                    // Make unselected seats available again
                    _logger.LogInformation("Making {Count} unselected seats available again", seatsToUnlock.Count);
                    await _repository.UpdateSeatAvailability(seatsToUnlock.Select(l => l.SeatId).ToList(), true, order.PresentationId);
                }

                // Get the currently locked seat IDs (without the ones we just unlocked)
                var lockedSeatIds = existingLocks
                    .Where(l => !seatsToUnlock.Contains(l))
                    .Select(l => l.SeatId)
                    .ToList();

                // Find seats that need a lock
                var seatsNeedingLocks = seatingOption.SeatIds
                    .Where(seatId => !lockedSeatIds.Contains(seatId))
                    .ToList();
                
                if (seatsNeedingLocks.Any())
                {
                    _logger.LogInformation("Creating new locks for {LockCount} seats for order {OrderToken}: {SeatIds}", 
                        seatsNeedingLocks.Count, request.OrderToken, string.Join(", ", seatsNeedingLocks));
                    
                    var newLocks = seatsNeedingLocks.Select(seatId => new SeatLock
                    {
                        SeatId = seatId,
                        OrderToken = order.OrderToken,
                        PresentationId = order.PresentationId,
                        TicketOrderId = order.Id,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                    }).ToList();
                    
                    var added = await _repository.AddSeatLocks(newLocks);
                    if (!added)
                    {
                        _logger.LogError("Failed to create locks for seats {SeatIds}", 
                            string.Join(", ", seatsNeedingLocks));
                    }
                }

                // Verify seats are still available (excluding our own remaining locks)
                if (!await _repository.AreSeatAvailable(order.PresentationId, seatingOption.SeatIds, order.OrderToken))
                {
                    _logger.LogError("Selected seats {SeatIds} are no longer available for order {OrderToken}, presentation {PresentationId}", 
                        string.Join(", ", seatingOption.SeatIds), request.OrderToken, order.PresentationId);
                    throw new SeatNotAvailableException("Selected seats are no longer available");
                }

                // Update order with selected seats and clear any existing items first
                order.Items = new List<TicketOrderItem>();
                foreach (var seatId in seatingOption.SeatIds)
                {
                    order.Items.Add(new TicketOrderItem
                    {
                        SeatId = seatId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                _logger.LogInformation("Updating order {OrderToken} with {SeatCount} selected seats: {SeatIds}", 
                    request.OrderToken, seatingOption.SeatIds.Count, string.Join(", ", seatingOption.SeatIds));

                if (!await _repository.SaveOrder(order))
                {
                    _logger.LogError("Failed to save order {OrderToken}", request.OrderToken);
                    throw new Exception("Failed to save order");
                }

                // After successfully updating the order, update seat availability for selected seats
                _logger.LogInformation("Marking {SeatCount} selected seats as unavailable for presentation {PresentationId}", 
                    seatingOption.SeatIds.Count, order.PresentationId);
                await _repository.UpdateSeatAvailability(seatingOption.SeatIds, false, order.PresentationId);
                
                _logger.LogInformation("Successfully selected option {Option} for order {OrderToken} with {SeatCount} seats: {SeatIds}", 
                    option, request.OrderToken, seatingOption.SeatIds.Count, string.Join(", ", seatingOption.SeatIds));

                return new OrderResponse(order.OrderToken, seatingOption.SeatIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting seating option: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<List<TicketResponse>> ConfirmOrder(Guid orderToken, ConfirmOrderRequest request)
        {
            try 
            {
                _logger.LogInformation("Starting to confirm order {OrderToken}", orderToken);
                
                var order = await _repository.GetOrderByToken(orderToken, true);
                if (order == null || order.Status != OrderStatus.Pending)
                {
                    _logger.LogWarning("Order {OrderToken} not found or expired", orderToken);
                    throw new OrderNotFoundException("Order not found or expired");
                }

                // Get the seat IDs from the order items
                var seatIds = order.Items.Select(i => i.SeatId).ToList();
                
                if (seatIds.Count == 0)
                {
                    _logger.LogWarning("Order {OrderToken} has no seats selected", orderToken);
                    throw new ArgumentException("No seats selected");
                }
                
                _logger.LogInformation("Confirming order {OrderToken} for presentation {PresentationId} with {SeatCount} seats: {SeatIds}", 
                    orderToken, order.PresentationId, seatIds.Count, string.Join(", ", seatIds));

                // Get the presentation to make sure it exists
                var presentation = await _repository.GetPresentationById(order.PresentationId);
                if (presentation == null)
                {
                    _logger.LogWarning("Presentation {PresentationId} not found for order {OrderToken}", 
                        order.PresentationId, orderToken);
                    throw new Exception($"Presentation {order.PresentationId} not found");
                }

                // Get all existing locks for this order
                _logger.LogInformation("Retrieving existing locks for order {OrderToken}", orderToken.ToString());
                var existingLocks = await _repository.GetLocksByOrder(orderToken.ToString());
                _logger.LogInformation("Found {Count} existing locks for order", existingLocks.Count);

                // Check if any seats don't have locks and create them if needed
                var seatsNeedingLocks = new List<int>();
                foreach (var seatId in seatIds)
                {
                    if (!existingLocks.Any(l => l.SeatId == seatId))
                    {
                        _logger.LogWarning("Seat {SeatId} is not locked for order {OrderToken} - will create lock", 
                            seatId, orderToken);
                        seatsNeedingLocks.Add(seatId);
                    }
                }
                
                // Create locks for any seats that don't have them
                if (seatsNeedingLocks.Any())
                {
                    var newLocks = seatsNeedingLocks.Select(seatId => new SeatLock
                    {
                        SeatId = seatId,
                        OrderToken = orderToken,
                        PresentationId = order.PresentationId,
                        TicketOrderId = order.Id,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                    }).ToList();
                    
                    var added = await _repository.AddSeatLocks(newLocks);
                    if (!added)
                    {
                        _logger.LogError("Failed to create locks for seats {SeatIds} for order {OrderToken}", 
                            string.Join(", ", seatsNeedingLocks), orderToken);
                    }
                    else
                    {
                        _logger.LogInformation("Created {LockCount} new locks for order {OrderToken}", 
                            newLocks.Count, orderToken);
                    }
                }

                // Verify seats are still available (excluding our own locks)
                if (!await _repository.AreSeatAvailable(order.PresentationId, seatIds, orderToken))
                {
                    _logger.LogError("Seats {SeatIds} are no longer available for order {OrderToken}, presentation {PresentationId}", 
                        string.Join(", ", seatIds), orderToken, order.PresentationId);
                    throw new SeatNotAvailableException("Some selected seats are no longer available");
                }

                // Update order status
                order.Status = OrderStatus.Confirmed;
                var saved = await _repository.SaveOrder(order);
                if (!saved)
                {
                    _logger.LogError("Failed to update order status to Confirmed for order {OrderToken}", orderToken);
                    throw new Exception("Failed to update order status");
                }

                // Create tickets for the order
                var tickets = new List<Ticket>();
                
                foreach (var item in order.Items)
                {
                    // Get the seat and presentation entities for this ticket
                    var seat = await _repository.GetSeatById(item.SeatId);
                    var ticketPresentation = await _repository.GetPresentationById(order.PresentationId);
                    
                    if (seat != null && ticketPresentation != null)
                    {
                        var ticket = new Ticket
                        {
                            PresentationId = order.PresentationId,
                            SeatId = item.SeatId,
                            TicketOrderId = order.Id,
                            CustomerName = request.CustomerName,
                            CustomerEmail = request.CustomerEmail,
                            Status = TicketStatus.Reserved,
                            PurchaseDate = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            Presentation = ticketPresentation,
                            Seat = seat,
                            TicketOrder = order
                        };
                        
                        tickets.Add(ticket);
                    }
                }

                // Create the tickets
                var createdTickets = await _repository.CreateTickets(tickets);
                
                _logger.LogInformation("Successfully confirmed order {OrderToken} with {TicketCount} tickets", 
                    orderToken.ToString(), createdTickets.Count);

                // Now, fetch the full ticket information including navigation properties for the response
                var fullTickets = new List<TicketResponse>();
                foreach (var ticket in createdTickets)
                {
                    // Load related entities for each ticket
                    var ticketPresentation = await _repository.GetPresentationById(ticket.PresentationId);
                    var seat = await _repository.GetSeatById(ticket.SeatId);
                    
                    if (ticketPresentation != null && seat != null)
                    {
                        fullTickets.Add(new TicketResponse(
                            ticket.Id,
                            ticketPresentation.Movie?.Title ?? "Unknown Movie",
                            ticketPresentation.HallName,
                            ticketPresentation.StartTime,
                            ticketPresentation.EndTime,
                            seat.RowNumber,
                            seat.SeatNumber,
                            ticket.CustomerName,
                            ticket.CustomerEmail,
                            ticket.Status,
                            ticket.PurchaseDate
                        ));
                    }
                }

                _logger.LogInformation("Returning {TicketCount} ticket details for order {OrderToken}", 
                    fullTickets.Count, orderToken);
                return fullTickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming order {OrderToken}: {ErrorMessage}", orderToken, ex.Message);
                throw;
            }
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
                    PresentationId = order.PresentationId,
                    OrderToken = orderToken,
                    TicketOrderId = order.Id,
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

                await _repository.UpdateSeatAvailability(request.SeatIds, false, order.PresentationId);

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

                // Get and remove seat locks for this specific seat
                var locks = await _repository.GetLocksByOrder(orderToken); 
                var seatLocks = locks.Where(l => l.SeatId == seatId).ToList();
                
                _logger.LogInformation("Removing {LockCount} locks for seat {SeatId} from order {OrderToken}", 
                    seatLocks.Count, seatId, orderToken);
                    
                await _repository.RemoveSeatLocks(seatLocks);

                if (!await _repository.SaveOrder(order))
                {
                    throw new Exception("Failed to save order");
                }

                // After successfully removing the seat, update its availability to available (true)
                _logger.LogInformation("Marking seat {SeatId} as available for presentation {PresentationId}", 
                    seatId, order.PresentationId);
                await _repository.UpdateSeatAvailability(new List<int> { seatId }, true, order.PresentationId);

                return new OrderResponse(order.OrderToken, order.Items.Select(i => i.SeatId).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing seat from order: {ErrorMessage}", ex.Message);
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

        public async Task FinalizeOrder(Guid orderToken)
        {
            var order = await _repository.FindTicketOrderByOrderToken(orderToken);

            if (order != null)
            {
                var tickets = await _repository.FindTicketsByOrderId(order.Id);
                var customerName = tickets.First().CustomerName;
                var customerEmail = tickets.First().CustomerEmail;
                var ticketBytes = await GetTicketsByOrderToken(orderToken);

                var attachments = new List<object>()
            {
                new MimePart("application", "pdf")
                {
                    Content = new MimeContent(new MemoryStream(ticketBytes)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = $"{orderToken}.pdf"
                }
            };

                _mailService.SendEmail(customerName, customerEmail, "Your tickets are here!", MailTemplates.OrderCompleteMailTemplate(customerName, order.Presentation, tickets.Count), attachments);

            } else
            {
                throw new OrderNotFoundException("No order found with the given order token");
            }
        }

        public async Task<byte[]> GetTicketsByOrderToken(Guid orderToken)
        {
            var ticketOrder = await _repository.FindTicketOrderByOrderToken(orderToken);

            if (ticketOrder == null)
            {
                throw new OrderNotFoundException("No order found with the given order token");
            }

            var tickets = await _repository.FindTicketsByOrderId(ticketOrder.Id);

            return _ticketPdfService.CreatePdfTicketsAsByteArray(tickets, ticketOrder.OrderToken);
        }

        public async Task<byte[]> GetTicketsByPhoneBookingCode(string phoneBookingCode)
        {
            var tickets = await _repository.FindTicketsByPhoneBookingCode(phoneBookingCode);
            if (!tickets.Any())
            {
                throw new TicketNotFoundException($"No tickets found with phone booking code {phoneBookingCode}");
            }
            
            return _ticketPdfService.CreatePdfTicketsAsByteArray(tickets, Guid.NewGuid());
        }

        public async Task UpdateSeatAvailability(List<int> seatIds, bool isAvailable, int presentationId)
        {
            await _repository.UpdateSeatAvailability(seatIds, isAvailable, presentationId);
        }
    }
}