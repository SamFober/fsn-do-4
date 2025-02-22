using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Models;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TicketsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(ApplicationDbContext context, ILogger<TicketsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Request/Response Models
        public record BookTicketRequest(
            int PresentationId,
            int SeatId = 0,
            string CustomerName = "",
            string CustomerEmail = ""
        );

        public record SeatInfo(int SeatId, int SeatNumber);
        
        public record SeatGroup(int RowNumber, List<SeatInfo> Seats);
        
        public record TicketResponse(
            int TicketId,
            string MovieTitle,
            string HallName,
            DateTime StartTime,
            DateTime EndTime,
            int Row,
            int SeatNumber,
            string CustomerName,
            string CustomerEmail,
            TicketStatus Status,
            DateTime PurchaseDate
        );

        [HttpPost]
        [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TicketResponse>> BookTicket(BookTicketRequest request)
        {
            try
            {
                var presentation = await _context.Presentations
                    .Include(p => p.Movie)
                    .Include(p => p.Hall)
                    .Include(p => p.Tickets.Where(t => t.Status != TicketStatus.Cancelled))
                    .FirstOrDefaultAsync(p => p.Id == request.PresentationId);

                if (presentation == null || presentation.Movie == null || presentation.Hall == null)
                {
                    return NotFound("Presentation not found or missing required data");
                }

                // If no seat specified, automatically assign one
                var seatId = request.SeatId;
                if (seatId == 0)
                {
                    var autoAssignResult = await GetAutomaticSeatAssignment(request.PresentationId, 1);
                    if (autoAssignResult.Result is OkObjectResult okResult && 
                        okResult.Value is List<SeatInfo> seatInfos && 
                        seatInfos.Any())
                    {
                        seatId = seatInfos.First().SeatId;
                    }
                    else
                    {
                        return BadRequest("No seats available for this presentation");
                    }
                }

                var seat = await _context.Seats
                    .FirstOrDefaultAsync(s => s.Id == seatId && s.HallId == presentation.HallId);

                if (seat == null)
                {
                    return BadRequest("Invalid seat for this hall");
                }

                var isBooked = await _context.Tickets
                    .AnyAsync(t => t.PresentationId == request.PresentationId 
                        && t.SeatId == seatId 
                        && t.Status != TicketStatus.Cancelled);

                if (isBooked)
                {
                    return BadRequest("Seat is already booked");
                }

                var ticket = new Ticket
                {
                    PresentationId = request.PresentationId,
                    SeatId = seatId,
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    Status = TicketStatus.Reserved,
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Presentation = presentation,
                    Seat = seat
                };

                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();

                return Ok(CreateTicketResponse(ticket, presentation));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error booking ticket");
                return StatusCode(500, "An error occurred while booking the ticket");
            }
        }

        [HttpPost("test/setup-split-scenario")]
        public async Task<ActionResult> SetupSplitScenario(int presentationId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var presentation = await _context.Presentations
                    .Include(p => p.Movie)
                    .Include(p => p.Hall)
                    .ThenInclude(h => h.Seats)
                    .FirstOrDefaultAsync(p => p.Id == presentationId);

                if (presentation == null)
                {
                    return NotFound("Presentation not found");
                }

                // First, cancel any existing tickets and remove locks
                var existingTickets = await _context.Tickets
                    .Where(t => t.PresentationId == presentationId)
                    .ToListAsync();
                foreach (var ticket in existingTickets)
                {
                    ticket.Status = TicketStatus.Cancelled;
                }

                var existingLocks = await _context.SeatLocks
                    .Where(l => l.OrderToken.ToString().Contains(presentationId.ToString()))
                    .ToListAsync();
                _context.SeatLocks.RemoveRange(existingLocks);

                var existingOrders = await _context.TicketOrders
                    .Where(o => o.PresentationId == presentationId)
                    .ToListAsync();
                foreach (var order in existingOrders)
                {
                    order.Status = OrderStatus.Cancelled;
                }

                await _context.SaveChangesAsync();

                // Get all seats in the hall
                var allSeats = presentation.Hall.Seats
                    .OrderBy(s => s.RowNumber)
                    .ThenBy(s => s.SeatNumber)
                    .ToList();

                // Create a specific pattern in row 5 for testing:
                // [B][B][B][_][_][B][B][_][_][B] (where B=Booked, _=Available)
                var row5 = allSeats.Where(s => s.RowNumber == 5).OrderBy(s => s.SeatNumber).ToList();
                var seatsToLeaveEmpty = new[] { 4, 5, 8, 9 }; // These seat numbers will be left available

                // Book all seats except:
                // 1. The specific pattern in row 5
                // 2. A few random seats in other rows for variety
                var seatsToBook = allSeats.Where(s => 
                    (s.RowNumber != 5) && // Not in row 5
                    (s.RowNumber % 3 != 0 || s.SeatNumber % 7 != 0) // Leave some random seats empty
                ).Concat(
                    row5.Where(s => !seatsToLeaveEmpty.Contains(s.SeatNumber)) // Add row 5 seats that should be booked
                ).ToList();

                var tickets = seatsToBook.Select(seat => new Ticket
                {
                    PresentationId = presentationId,
                    SeatId = seat.Id,
                    CustomerName = "Test Booking",
                    CustomerEmail = "test@example.com",
                    Status = TicketStatus.Reserved,
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Presentation = presentation,
                    Seat = seat
                }).ToList();

                await _context.Tickets.AddRangeAsync(tickets);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Get the available seats after booking
                var availableSeats = allSeats
                    .Where(s => !seatsToBook.Contains(s))
                    .OrderBy(s => s.RowNumber)
                    .ThenBy(s => s.SeatNumber)
                    .ToList();

                return Ok(new
                {
                    Message = "Test scenario created",
                    TotalSeatsBooked = tickets.Count,
                    TotalSeatsAvailable = availableSeats.Count,
                    AvailableSeatsInRow5 = availableSeats
                        .Where(s => s.RowNumber == 5)
                        .Select(s => new { s.RowNumber, s.SeatNumber, s.Id })
                        .OrderBy(s => s.SeatNumber)
                        .ToList(),
                    Pattern = "Row 5: [B][B][B][_][_][B][B][_][_][B]"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error setting up test scenario");
                return StatusCode(500, $"An error occurred while setting up the test scenario: {ex.Message}");
            }
        }

        [HttpGet("auto-assign")]
        public async Task<ActionResult<List<SeatInfo>>> GetAutomaticSeatAssignment(
            int presentationId, 
            [FromQuery] int numberOfSeats = 1)
        {
            try
            {
                if (numberOfSeats <= 0)
                {
                    return BadRequest("Number of seats must be greater than 0");
                }

                // Try to find consecutive seats first
                var consecutiveSeats = await FindBestConsecutiveSeats(presentationId, numberOfSeats);
                if (consecutiveSeats.Any())
                {
                    var seats = await _context.Seats
                        .Where(s => consecutiveSeats.Contains(s.Id))
                        .Select(s => new SeatInfo(s.Id, s.SeatNumber))
                        .ToListAsync();
                    return Ok(seats);
                }

                // If no consecutive seats, try split seating
                var splitSeats = await FindBestSplitSeats(presentationId, numberOfSeats);
                if (splitSeats.Any())
                {
                    var seats = await _context.Seats
                        .Where(s => splitSeats.Contains(s.Id))
                        .Select(s => new SeatInfo(s.Id, s.SeatNumber))
                        .ToListAsync();
                    return Ok(seats);
                }

                return BadRequest($"Could not find {numberOfSeats} available seats");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning automatic seats");
                return StatusCode(500, "An error occurred while assigning seats");
            }
        }

        // Helper methods
        private Dictionary<int, List<Seat>> GetAvailableSeatsPerRow(IEnumerable<Seat> allSeats, HashSet<int> occupiedSeatIds)
        {
            return allSeats
                .Where(s => !occupiedSeatIds.Contains(s.Id))
                .GroupBy(s => s.RowNumber)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(s => s.SeatNumber).ToList()
                );
        }

        private List<Seat>? FindBestConsecutiveSeatsWithMiddlePreference(
            Dictionary<int, List<Seat>> availableSeatsPerRow,
            int numberOfSeats,
            int totalSeatsInRow)
        {
            var middleSeat = totalSeatsInRow / 2;
            
            // Try each row, starting from the middle rows
            foreach (var row in availableSeatsPerRow.OrderBy(r => Math.Abs(r.Key - availableSeatsPerRow.Count / 2)))
            {
                var seats = row.Value;
                
                // Try to find seats centered around the middle
                for (int offset = 0; offset <= totalSeatsInRow / 2; offset++)
                {
                    // Try left of middle
                    var leftStart = middleSeat - offset - numberOfSeats + 1;
                    if (leftStart >= 0)
                    {
                        var consecutiveSeats = FindConsecutiveSeats(seats, leftStart, numberOfSeats);
                        if (consecutiveSeats != null) return consecutiveSeats;
                    }

                    // Try right of middle
                    var rightStart = middleSeat + offset;
                    if (rightStart + numberOfSeats <= totalSeatsInRow)
                    {
                        var consecutiveSeats = FindConsecutiveSeats(seats, rightStart, numberOfSeats);
                        if (consecutiveSeats != null) return consecutiveSeats;
                    }
                }
            }

            return null;
        }

        private List<Seat>? FindConsecutiveSeats(List<Seat> seats, int startSeatNumber, int numberOfSeats)
        {
            var consecutive = new List<Seat>();
            var currentSeatNumber = startSeatNumber;

            foreach (var seat in seats.Where(s => s.SeatNumber >= startSeatNumber).OrderBy(s => s.SeatNumber))
            {
                if (seat.SeatNumber != currentSeatNumber)
                {
                    return null; // Gap found
                }
                consecutive.Add(seat);
                if (consecutive.Count == numberOfSeats)
                {
                    return consecutive;
                }
                currentSeatNumber++;
            }

            return null; // Not enough consecutive seats
        }

        private List<List<Seat>> FindBestSplitOptions(Dictionary<int, List<Seat>> availableSeatsPerRow, int numberOfSeats)
        {
            // First try to find the largest possible groups
            var result = new List<List<Seat>>();
            var remainingSeats = numberOfSeats;
            var allAvailableSeats = availableSeatsPerRow
                .SelectMany(r => r.Value)
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => Math.Abs(s.SeatNumber - (availableSeatsPerRow.Values.First().Count / 2))) // Prefer middle seats
                .ToList();

            // If we have enough total seats, but couldn't find consecutive ones,
            // just take the best individual seats
            if (allAvailableSeats.Count >= numberOfSeats)
            {
                // Take the best seats (preferring middle seats in each row)
                var bestSeats = allAvailableSeats.Take(numberOfSeats).ToList();
                
                // Group them by row for better presentation
                return bestSeats
                    .GroupBy(s => s.RowNumber)
                    .Select(g => g.ToList())
                    .ToList();
            }

            return new List<List<Seat>>();
        }

        private TicketResponse CreateTicketResponse(Ticket ticket, Presentation presentation)
        {
            if (presentation.Movie == null || presentation.Hall == null)
            {
                throw new InvalidOperationException("Presentation must have Movie and Hall loaded");
            }

            if (ticket.Seat == null)
            {
                throw new InvalidOperationException("Ticket must have Seat loaded");
            }

            return new TicketResponse(
                ticket.Id,
                presentation.Movie.Title,
                presentation.Hall.Name,
                presentation.StartTime,
                presentation.EndTime,
                ticket.Seat.RowNumber,
                ticket.Seat.SeatNumber,
                ticket.CustomerName,
                ticket.CustomerEmail,
                ticket.Status,
                ticket.PurchaseDate
            );
        }

        // Start a new order
        [HttpPost("orders/start")]
        public async Task<ActionResult<OrderResponse>> StartOrder(StartOrderRequest request)
        {
            try
            {
                // Create new order with auto-assigned seat
                var autoAssignResult = await GetAutomaticSeatAssignment(request.PresentationId, 1);
                if (autoAssignResult.Result is not OkObjectResult okResult || 
                    okResult.Value is not List<SeatInfo> seats ||
                    !seats.Any())
                {
                    return BadRequest("No seats available");
                }

                // Try to lock the seats
                var orderToken = Guid.NewGuid();
                var seatIds = seats.Select(s => s.SeatId).ToList();
                if (!await TryLockSeats(seatIds, orderToken, request.PresentationId))
                {
                    return BadRequest("Selected seats are no longer available");
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
                            SeatId = seats[0].SeatId,
                            CreatedAt = DateTime.UtcNow
                        }
                    }
                };

                _context.TicketOrders.Add(order);
                await _context.SaveChangesAsync();

                return Ok(new OrderResponse(order.OrderToken, order.Items.Select(i => i.SeatId).ToList()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting order");
                return StatusCode(500, "An error occurred");
            }
        }

        // Add seats to order
        [HttpPost("orders/{orderToken}/seats")]
        public async Task<ActionResult<OrderResponse>> AddSeatsToOrder(
            Guid orderToken, 
            AddSeatsRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.TicketOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.OrderToken == orderToken);

                if (order == null || order.Status != OrderStatus.Pending)
                    return NotFound("Order not found or expired");

                // Validate seats exist and belong to the same hall
                var seats = await _context.Seats
                    .Where(s => request.SeatIds.Contains(s.Id))
                    .ToListAsync();

                if (seats.Count != request.SeatIds.Count)
                    return BadRequest("One or more invalid seat IDs");

                // Check if seats are available
                if (!await ValidateSeatsAvailable(order.PresentationId, request.SeatIds))
                    return BadRequest("One or more seats are already taken");

                // Add new seats and create locks
                var timestamp = DateTime.UtcNow;
                foreach (var seatId in request.SeatIds)
                {
                    // Check for duplicates in current order
                    if (order.Items.Any(i => i.SeatId == seatId))
                        return BadRequest($"Seat {seatId} is already in your order");

                    // Add order item
                    order.Items.Add(new TicketOrderItem
                    {
                        SeatId = seatId,
                        CreatedAt = DateTime.UtcNow
                    });

                    // Create seat lock
                    _context.SeatLocks.Add(new SeatLock
                    {
                        SeatId = seatId,
                        OrderToken = orderToken,
                        CreatedAt = timestamp,
                        ExpiresAt = timestamp.AddMinutes(10)
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new OrderResponse(order.OrderToken, order.Items.Select(i => i.SeatId).ToList()));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error adding seats");
                return StatusCode(500, "An error occurred");
            }
        }

        // Remove seats from order
        [HttpDelete("orders/{orderToken}/seats/{seatId}")]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderResponse>> RemoveSeatFromOrder(Guid orderToken, int seatId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.TicketOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.OrderToken == orderToken);

                if (order == null || order.Status != OrderStatus.Pending)
                    return NotFound("Order not found or expired");

                // Don't allow removing the last seat
                if (order.Items.Count <= 1)
                    return BadRequest("Cannot remove the last seat from the order");

                var itemToRemove = order.Items.FirstOrDefault(i => i.SeatId == seatId);
                if (itemToRemove == null)
                    return NotFound("Seat not found in order");

                // Remove the seat lock
                var seatLock = await _context.SeatLocks
                    .FirstOrDefaultAsync(l => l.OrderToken == orderToken && l.SeatId == seatId);
                if (seatLock != null)
                {
                    _context.SeatLocks.Remove(seatLock);
                }

                // Remove the order item
                order.Items.Remove(itemToRemove);
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new OrderResponse(order.OrderToken, order.Items.Select(i => i.SeatId).ToList()));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error removing seat");
                return StatusCode(500, "An error occurred");
            }
        }

        /// <summary>
        /// Starts a new group booking process
        /// </summary>
        /// <param name="request">Contains PresentationId and NumberOfSeats</param>
        /// <returns>
        /// - Consecutive seats if available
        /// - Split seating options if consecutive seats aren't available
        /// - Bad request if no seats available or invalid input
        /// </returns>
        /// <response code="200">Returns seating options</response>
        /// <response code="400">If the request is invalid or no seats available</response>
        /// <response code="404">If the presentation is not found</response>
        [HttpPost("orders/start-group")]
        [ProducesResponseType(typeof(GroupOrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<GroupOrderResponse>> StartGroupOrder(StartGroupOrderRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate input
                if (request.NumberOfSeats <= 0)
                    return BadRequest("Number of seats must be positive");
                if (request.NumberOfSeats > 20)
                    return BadRequest("Group size too large");

                _logger.LogInformation("Starting group order for {NumberOfSeats} seats in presentation {PresentationId}", 
                    request.NumberOfSeats, request.PresentationId);

                // Validate presentation
                var presentation = await _context.Presentations
                    .Include(p => p.Hall)
                    .FirstOrDefaultAsync(p => p.Id == request.PresentationId);
                    
                if (presentation == null)
                {
                    _logger.LogWarning("Presentation {PresentationId} not found", request.PresentationId);
                    return NotFound("Presentation not found");
                }

                // Create the order object but don't save it yet
                var orderToken = Guid.NewGuid();
                var order = new TicketOrder
                {
                    OrderToken = orderToken,
                    PresentationId = request.PresentationId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Status = OrderStatus.Pending,
                    AvailableOptions = new Dictionary<string, SeatingOption>()
                };

                // First try consecutive seats
                var consecutiveSeats = await FindBestConsecutiveSeats(request.PresentationId, request.NumberOfSeats);
                _logger.LogInformation("Found {Count} consecutive seats", consecutiveSeats.Count);

                if (consecutiveSeats.Any())
                {
                    // Try to lock the seats immediately for consecutive booking
                    if (!await TryLockSeats(consecutiveSeats, orderToken, request.PresentationId))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest("Selected seats are no longer available");
                    }

                    // Create order items directly - no need for AvailableOptions
                    foreach (var seatId in consecutiveSeats)
                    {
                        order.Items.Add(new TicketOrderItem
                        {
                            SeatId = seatId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    // Only save the order now that we know we need it
                    _context.TicketOrders.Add(order);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new GroupOrderResponse(
                        orderToken,
                        true,
                        consecutiveSeats,
                        $"Found {consecutiveSeats.Count} consecutive seats",
                        null  // No alternatives needed
                    ));
                }

                // If we need split seating, save the order with options
                var splitSeats = await FindBestSplitSeats(request.PresentationId, request.NumberOfSeats);
                if (splitSeats.Any())
                {
                    var maxConsecutive = await FindMaxConsecutiveSeats(request.PresentationId);
                    var splitArrangement = await GetSplitArrangement(splitSeats);
                    var options = new List<GroupSeatingOption>();
                    
                    // Store options for later selection
                    order.AvailableOptions["split"] = new SeatingOption
                    {
                        Type = "split",
                        SeatIds = splitSeats,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                    };

                    // Add split seating option to the options list
                    options.Add(new GroupSeatingOption(
                        "split",
                        request.NumberOfSeats,
                        splitArrangement,
                        $"Keep {request.NumberOfSeats} tickets split across {splitArrangement.Count} rows"
                    ));

                    // If there are some consecutive seats available, add that option too
                    if (maxConsecutive >= 3)
                    {
                        var smallerConsecutiveSeats = await FindBestConsecutiveSeats(request.PresentationId, maxConsecutive);
                        order.AvailableOptions["smaller_consecutive"] = new SeatingOption
                        {
                            Type = "consecutive",
                            SeatIds = smallerConsecutiveSeats,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                        };

                        options.Add(new GroupSeatingOption(
                            "consecutive",
                            maxConsecutive,
                            new List<RowGroup> { 
                                new(
                                    await GetRowNumber(smallerConsecutiveSeats[0]),
                                    smallerConsecutiveSeats,
                                    $"{maxConsecutive} seats together in row {await GetRowNumber(smallerConsecutiveSeats[0])}"
                                )
                            },
                            $"Reduce group size to {maxConsecutive} to sit together"
                        ));
                    }

                    // Save the order with options
                    _context.TicketOrders.Add(order);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new GroupOrderResponse(
                        orderToken,
                        false,
                        splitSeats,
                        "No consecutive seats available for this group size",
                        new GroupOrderAlternatives(
                            maxConsecutive,
                            "Please choose your preferred seating arrangement",
                            options  // Now we're passing the populated options list
                        )
                    ));
                }

                // No seats available - no need to save anything
                await transaction.RollbackAsync();
                return BadRequest("No seats available");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in StartGroupOrder: {Message}", ex.Message);
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Selects a specific seating option for a group booking
        /// </summary>
        /// <param name="option">The type of seating arrangement ("split" or "consecutive")</param>
        /// <param name="request">Contains the OrderToken from the initial group booking</param>
        /// <returns>Confirmed seats for the selected arrangement</returns>
        /// <response code="200">Returns the confirmed seat selection</response>
        /// <response code="400">If the option is invalid or expired</response>
        [HttpPost("orders/start-group/{option}")]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OrderResponse>> StartGroupOrderWithOption(
            string option,
            [FromBody] StartGroupOptionRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.TicketOrders
                    .Include(o => o.Presentation)
                    .FirstOrDefaultAsync(o => o.OrderToken == request.OrderToken);

                if (order == null || !order.AvailableOptions.ContainsKey(option))
                {
                    return BadRequest("Invalid option or order not found");
                }

                var selectedOption = order.AvailableOptions[option];
                if (selectedOption.ExpiresAt < DateTime.UtcNow)
                {
                    return BadRequest("Seating option has expired");
                }

                // Try to lock exactly these seats
                if (!await TryLockSeats(selectedOption.SeatIds, request.OrderToken, order.PresentationId))
                {
                    return BadRequest("Selected seats are no longer available");
                }

                // Create order items
                foreach (var seatId in selectedOption.SeatIds)
                {
                    order.Items.Add(new TicketOrderItem
                    {
                        SeatId = seatId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new OrderResponse(order.OrderToken, selectedOption.SeatIds));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing group order option");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        private async Task<int> FindMaxConsecutiveSeats(int presentationId)
        {
            var allSeats = await _context.Seats
                .Where(s => s.Hall.Presentations.Any(p => p.Id == presentationId))
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            var takenSeats = await GetUnavailableSeats(presentationId);
            var maxConsecutive = 0;
            var currentConsecutive = 0;
            var lastRowNumber = -1;
            var lastSeatNumber = -1;

            foreach (var seat in allSeats)
            {
                if (takenSeats.Contains(seat.Id) || 
                    seat.RowNumber != lastRowNumber || 
                    seat.SeatNumber != lastSeatNumber + 1)
                {
                    maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                    currentConsecutive = 0;
                }

                if (!takenSeats.Contains(seat.Id))
                {
                    currentConsecutive++;
                }

                lastRowNumber = seat.RowNumber;
                lastSeatNumber = seat.SeatNumber;
            }

            maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
            return maxConsecutive;
        }

        // Update the response record to include alternatives
        public record GroupOrderAlternatives(
            int MaxConsecutiveSeats,
            string Suggestion,
            List<GroupSeatingOption> Options
        );

        public record GroupOrderResponse(
            Guid OrderToken,
            bool HasConsecutiveSeats,
            List<int> SeatIds,
            string Message,
            GroupOrderAlternatives? Alternatives
        );

        public record GroupSeatingOption(
            string Type,  // "consecutive" or "split"
            int TotalSeats,
            List<RowGroup> Arrangement,
            string Description
        );

        public record RowGroup(
            int RowNumber,
            List<int> SeatIds,
            string Description  // e.g., "3 seats in row 5"
        );

        private async Task<List<int>> FindBestConsecutiveSeats(int presentationId, int numberOfSeats)
        {
            // Get all seats for this presentation
            var allSeats = await _context.Seats
                .Where(s => s.Hall.Presentations.Any(p => p.Id == presentationId))
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            // Get already taken seats
            var takenSeats = await GetUnavailableSeats(presentationId);

            // Find best consecutive seats (middle of the row preferred)
            var bestSeats = FindBestConsecutiveSeatsInRows(allSeats, takenSeats, numberOfSeats);
            
            return bestSeats;
        }

        private List<int> FindBestConsecutiveSeatsInRows(List<Seat> allSeats, HashSet<int> takenSeats, int numberOfSeats)
        {
            var bestSeats = new List<int>();
            var currentSeats = new List<int>();

            foreach (var seat in allSeats)
            {
                if (takenSeats.Contains(seat.Id))
                {
                    currentSeats.Clear();
                }
                else
                {
                    currentSeats.Add(seat.Id);
                    if (currentSeats.Count == numberOfSeats)
                    {
                        bestSeats = currentSeats.ToList();
                        currentSeats.RemoveAt(0);
                    }
                }
            }

            return bestSeats;
        }

        private async Task<HashSet<int>> GetUnavailableSeats(int presentationId)
        {
            var bookedSeats = await _context.Tickets
                .Where(t => t.PresentationId == presentationId && 
                       t.Status != TicketStatus.Cancelled)
                .Select(t => t.SeatId)
                .ToListAsync();

            var pendingSeats = await _context.TicketOrders
                .Where(o => o.PresentationId == presentationId && 
                       o.Status == OrderStatus.Pending &&
                       o.ExpiresAt > DateTime.UtcNow)
                .SelectMany(o => o.Items.Select(i => i.SeatId))
                .ToListAsync();

            var unavailableSeats = new HashSet<int>(bookedSeats);
            unavailableSeats.UnionWith(pendingSeats);
            return unavailableSeats;
        }

        /// <summary>
        /// Confirms a booking and creates tickets
        /// </summary>
        /// <param name="orderToken">The unique identifier for the order</param>
        /// <param name="request">Customer details for the tickets</param>
        /// <returns>List of created tickets</returns>
        /// <response code="200">Returns the created tickets</response>
        /// <response code="400">If the seats are no longer available</response>
        /// <response code="404">If the order is not found or expired</response>
        [HttpPost("orders/{orderToken}/confirm")]
        [ProducesResponseType(typeof(List<TicketResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<TicketResponse>>> ConfirmOrder(
            Guid orderToken,
            ConfirmOrderRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.TicketOrders
                    .Include(o => o.Items)
                    .Include(o => o.Presentation) // Load Presentation for null-check
                    .FirstOrDefaultAsync(o => o.OrderToken == orderToken);

                if (order?.Presentation == null || order.Status != OrderStatus.Pending)
                    return NotFound("Order not found or expired");

// Now explicitly load related entities
                await _context.Entry(order.Presentation).Reference(p => p.Movie).LoadAsync();
                await _context.Entry(order.Presentation).Reference(p => p.Hall).LoadAsync();

// Now you can safely access order.Presentation.Movie and order.Presentation.Hall

                // Remove all seat locks for this order
                var seatLocks = await _context.SeatLocks
                    .Where(l => l.OrderToken == orderToken)
                    .ToListAsync();
                _context.SeatLocks.RemoveRange(seatLocks);

                // Verify seats are still available
                var bookedSeats = await _context.Tickets
                    .Where(t => t.PresentationId == order.PresentationId && 
                           t.Status != TicketStatus.Cancelled)
                    .Select(t => t.SeatId)
                    .ToListAsync();

                var orderSeatIds = order.Items.Select(i => i.SeatId);
                if (bookedSeats.Any(id => orderSeatIds.Contains(id)))
                {
                    return BadRequest("Some selected seats are no longer available");
                }

                // Create tickets for each seat
                var seats = await _context.Seats
                    .Where(s => orderSeatIds.Contains(s.Id))
                    .ToListAsync();

                var tickets = seats.Select(seat => new Ticket
                {
                    PresentationId = order.PresentationId,
                    SeatId = seat.Id,
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    Status = TicketStatus.Reserved,
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Presentation = order.Presentation,
                    Seat = seat
                }).ToList();

                // Update order status
                order.Status = OrderStatus.Confirmed;

                await _context.Tickets.AddRangeAsync(tickets);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok(tickets.Select(t => CreateTicketResponse(t, order.Presentation)).ToList());
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error confirming order");
                return StatusCode(500, "An error occurred");
            }
        }

        private async Task<bool> ValidateSeatsAvailable(int presentationId, List<int> seatIds)
        {
            var bookedSeats = await _context.Tickets
                .Where(t => t.PresentationId == presentationId && 
                       t.Status != TicketStatus.Cancelled)
                .Select(t => t.SeatId)
                .ToListAsync();

            var lockedSeats = await _context.SeatLocks
                .Where(l => l.ExpiresAt > DateTime.UtcNow && 
                       seatIds.Contains(l.SeatId))
                .Select(l => l.SeatId)
                .ToListAsync();

            var pendingSeats = await _context.TicketOrders
                .Where(o => o.PresentationId == presentationId && 
                       o.Status == OrderStatus.Pending &&
                       o.ExpiresAt > DateTime.UtcNow)
                .SelectMany(o => o.Items.Select(i => i.SeatId))
                .ToListAsync();

            var unavailableSeats = bookedSeats.Concat(lockedSeats).Concat(pendingSeats).ToHashSet();
            return !seatIds.Any(id => unavailableSeats.Contains(id));
        }

        public record StartOrderRequest(int PresentationId);
        public record StartGroupOrderRequest(int PresentationId, int NumberOfSeats);
        public record AddSeatsRequest(List<int> SeatIds);
        public record ConfirmOrderRequest(string CustomerName, string CustomerEmail);
        public record OrderResponse(Guid OrderToken, List<int> SeatIds);
        public record StartGroupOptionRequest(Guid OrderToken);

        private async Task<TicketOrder> CreateOrder(int presentationId, List<int> seatIds, Guid orderToken)
        {
            var order = new TicketOrder
            {
                OrderToken = orderToken,
                PresentationId = presentationId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Status = OrderStatus.Pending
            };

            foreach (var seatId in seatIds)
            {
                order.Items.Add(new TicketOrderItem
                {
                    SeatId = seatId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.TicketOrders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        private async Task<List<int>> FindBestSplitSeats(int presentationId, int numberOfSeats)
        {
            var allSeats = await _context.Seats
                .Where(s => s.Hall.Presentations.Any(p => p.Id == presentationId))
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            var takenSeats = await GetUnavailableSeats(presentationId);
            
            // Try to minimize splits across rows
            var bestArrangement = allSeats
                .Where(s => !takenSeats.Contains(s.Id))
                .GroupBy(s => s.RowNumber)
                .OrderByDescending(g => g.Count())
                .SelectMany(g => g)
                .Take(numberOfSeats)
                .ToList();

            // If we can't find enough seats
            if (bestArrangement.Count < numberOfSeats)
                return new List<int>();

            return bestArrangement.Select(s => s.Id).ToList();
        }

        private async Task<int> GetRowNumber(int seatId)
        {
            return await _context.Seats
                .Where(s => s.Id == seatId)
                .Select(s => s.RowNumber)
                .FirstAsync();
        }

        private async Task<List<RowGroup>> GetSplitArrangement(List<int> seatIds)
        {
            var seats = await _context.Seats
                .Where(s => seatIds.Contains(s.Id))
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            if (!seats.Any())
            {
                return new List<RowGroup>();
            }

            var result = new List<RowGroup>();
            
            // Group seats by row number
            foreach (var rowGroup in seats.GroupBy(s => s.RowNumber))
            {
                if (rowGroup == null) continue;
                
                var rowNumber = rowGroup.Key;
                var rowSeatIds = rowGroup.Select(s => s.Id).ToList();
                var seatCount = rowGroup.Count();
                
                if (rowSeatIds.Any())
                {
                    result.Add(new RowGroup(
                        rowNumber,
                        rowSeatIds,
                        $"{seatCount} seats in row {rowNumber}"
                    ));
                }
            }

            return result;
        }

        private async Task<bool> TryLockSeats(List<int> seatIds, Guid orderToken, int presentationId)
        {
            try
            {
                // Check if seats are still available
                if (!await ValidateSeatsAvailable(presentationId, seatIds))
                {
                    return false;
                }

                // Lock seats with orderToken
                var timestamp = DateTime.UtcNow;
                foreach (var seatId in seatIds)
                {
                    _context.SeatLocks.Add(new SeatLock
                    {
                        SeatId = seatId,
                        OrderToken = orderToken,
                        CreatedAt = timestamp,
                        ExpiresAt = timestamp.AddMinutes(10)
                    });
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking seats");
                throw;
            }
        }
    }
} 