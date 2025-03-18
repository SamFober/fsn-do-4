using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Models;
using WebApi.Interfaces.Services;
using WebApi.Models.Requests;
using WebApi.Models.Responses;
using WebApi.Exceptions;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;
        private readonly ILogger<TicketsController> _logger;
        private readonly ApplicationDbContext _context; // Keep for test setup only

        public TicketsController(
            ITicketService ticketService,
            ApplicationDbContext context,
            ILogger<TicketsController> logger)
        {
            _ticketService = ticketService;
            _context = context;
            _logger = logger;
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
                    return new NotFoundObjectResult("Presentation not found");
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

                // Reset all seats to available first
                foreach (var seat in presentation.Hall.Seats)
                {
                    seat.IsAvailable = true;
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

                // Set IsAvailable = false for all seats that will be booked
                foreach (var seat in seatsToBook)
                {
                    seat.IsAvailable = false;
                }
                await _context.SaveChangesAsync();

                // Create a mock ticket order for our test tickets
                var mockOrder = new TicketOrder
                {
                    OrderToken = Guid.NewGuid(),
                    PresentationId = presentationId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(1),
                    Status = OrderStatus.Confirmed
                };
                _context.TicketOrders.Add(mockOrder);
                await _context.SaveChangesAsync();

                var tickets = seatsToBook.Select(seat => new Ticket
                {
                    PresentationId = presentationId,
                    SeatId = seat.Id,
                    TicketOrderId = mockOrder.Id,
                    CustomerName = "Test Booking",
                    CustomerEmail = "test@example.com",
                    Status = TicketStatus.Reserved,
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Presentation = presentation,
                    Seat = seat,
                    TicketOrder = mockOrder
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

                return new OkObjectResult(new
                {
                    AvailableSeats = availableSeats.Select(s => new { s.Id, s.RowNumber, s.SeatNumber }),
                    BookedSeats = seatsToBook.Select(s => new { s.Id, s.RowNumber, s.SeatNumber })
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error setting up split scenario");
                return new ObjectResult("An error occurred") { StatusCode = 500 };
            }
        }

        /// <summary>
        /// Starts a new ticket order with automatic best seat selection
        /// </summary>
        /// <param name="request">The presentation ID to book tickets for</param>
        /// <remarks>
        /// The system automatically selects the best available seat based on:
        /// - Optimal viewing distance (2/3 back from screen)
        /// - Center positioning
        /// - Viewing angle
        /// - Distance from edges
        /// 
        /// Front row and corner seats receive penalties in the selection algorithm.
        /// </remarks>
        /// <response code="200">Returns the order token and selected seat</response>
        /// <response code="400">If no seats are available</response>
        [HttpPost("orders/start")]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OrderResponse>> StartOrder([FromBody] StartOrderRequest request)
        {
            try
            {
                var response = await _ticketService.StartOrder(request);
                return new OkObjectResult(response);
            }
            catch (NoSeatsAvailableException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting order");
                return new ObjectResult("An error occurred") { StatusCode = 500 };
            }
        }

        // Add seats to order
        [HttpPost("orders/{orderToken}/seats")]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderResponse>> AddSeatsToOrder(
            Guid orderToken, 
            [FromBody] AddSeatsRequest request)
        {
            try
            {
                var response = await _ticketService.AddSeatsToOrder(orderToken, request);
                return new OkObjectResult(response);
            }
            catch (OrderNotFoundException)
            {
                return new NotFoundObjectResult("Order not found");
            }
            catch (SeatNotAvailableException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding seats to order");
                return new ObjectResult("An error occurred") { StatusCode = 500 };
            }
        }

        // Remove seats from order
        [HttpDelete("orders/{orderToken}/seats/{seatId}")]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderResponse>> RemoveSeatFromOrder(string orderToken, int seatId)
        {
            Guid orderTokenGuid;
            if (!Guid.TryParse(orderToken, out orderTokenGuid))
            {
                return BadRequest("Invalid order token format.");
            }

            var response = new OrderResponse(); // Create a new response object
            try
            {
                var seat = await _context.Seats.FindAsync(seatId);
                if (seat != null)
                {
                    // Logic to remove the seat from the order
                    var orderResponse = await _ticketService.RemoveSeatFromOrder(orderTokenGuid, seatId); // Use the Guid

                    if (orderResponse.IsSuccess)
                    {
                        // Remove the seat from the seat lock
                        var seatLock = await _context.SeatLocks
                            .FirstOrDefaultAsync(sl => sl.SeatId == seat.Id && sl.OrderToken == orderTokenGuid);
                        if (seatLock != null)
                        {
                            _context.SeatLocks.Remove(seatLock);
                            await _context.SaveChangesAsync(); // Save changes to the database
                        }
                        string message = $"Seat {seat.SeatNumber} removed from the order and seat lock.";
                        return Ok(new { message }); // Return the message in the response
                    }
                }
            }
            catch (OrderNotFoundException)
            {
                return NotFound("Order not found");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            return BadRequest("Failed to remove seat.");
        }

        /// <summary>
        /// Starts a group order with automatic seat selection
        /// </summary>
        /// <param name="request">Contains presentation ID and number of seats needed</param>
        /// <remarks>
        /// The algorithm attempts to:
        /// 1. Find consecutive seats in optimal rows
        /// 2. If not possible, provides split seating options while maintaining:
        ///    - Minimal group separation
        ///    - Similar viewing quality for all seats
        ///    - Optimal positioning for each subgroup
        /// </remarks>
        /// <response code="200">Returns the order token and selected seating options</response>
        /// <response code="400">If no seats are available or invalid input</response>
        [HttpPost("orders/start-group")]
        [ProducesResponseType(typeof(GroupOrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<GroupOrderResponse>> StartGroupOrder([FromBody] StartGroupOrderRequest request)
        {
            try
            {
                if (request.PresentationId <= 0)
                {
                    return NotFound("Presentation not found");
                }

                if (request.NumberOfSeats <= 0 || request.NumberOfSeats > 20)
                {
                    return BadRequest("Invalid number of seats requested");
                }

                var response = await _ticketService.StartGroupOrder(request);
                if (response == null)
                {
                    return BadRequest("No seats available");
                }

                // Lock all potential seats across all options
                var allPotentialSeatIds = response.AvailableOptions.Values
                    .SelectMany(o => o.SeatIds)
                    .Distinct()
                    .ToList();

                var success = await TryLockSeats(allPotentialSeatIds, response.OrderToken, request.PresentationId);
                if (!success)
                {
                    return BadRequest("Failed to lock seats");
                }

                return Ok(response);
            }
            catch (NoSeatsAvailableException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting group order");
                return StatusCode(500, "An error occurred");
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
        [HttpPost("orders/select-option/{option}")]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderResponse>> SelectGroupSeatingOption(
            string option, 
            [FromBody] StartGroupOptionRequest request)
        {
            try
            {
                // Get the order to find all locked seats
                var order = await _context.TicketOrders
                    .FirstOrDefaultAsync(o => o.OrderToken == request.OrderToken);
                
                if (order == null)
                {
                    return NotFound("Order not found or expired");
                }

                // Get all currently locked seats for this order
                var lockedSeats = await _context.SeatLocks
                    .Where(l => l.OrderToken == request.OrderToken)
                    .ToListAsync();

                var response = await _ticketService.SelectGroupSeatingOption(option, request);
                if (response == null)
                {
                    return NotFound("Order not found or expired");
                }

                // Find seats that were locked but not selected in the chosen option
                var seatsToUnlock = lockedSeats
                    .Where(l => !response.SeatIds.Contains(l.SeatId))
                    .ToList();

                if (seatsToUnlock.Any())
                {
                    // Remove locks for unselected seats
                    _context.SeatLocks.RemoveRange(seatsToUnlock);
                    await _context.SaveChangesAsync();

                    // Make unselected seats available again
                    var seatIdsToMakeAvailable = seatsToUnlock.Select(l => l.SeatId).ToList();
                    var seatsToMakeAvailable = await _context.Seats
                        .Where(s => seatIdsToMakeAvailable.Contains(s.Id))
                        .ToListAsync();

                    foreach (var seat in seatsToMakeAvailable)
                    {
                        seat.IsAvailable = true;
                    }
                    await _context.SaveChangesAsync();
                }

                return Ok(response);
            }
            catch (OrderNotFoundException)
            {
                return NotFound("Order not found or expired");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
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
            [FromBody] ConfirmOrderRequest request)
        {
            try
            {
                var tickets = await _ticketService.ConfirmOrder(orderToken, request);
                if (tickets == null || !tickets.Any())
                {
                    return NotFound("Order not found or expired");
                }
                return Ok(tickets);
            }
            catch (OrderNotFoundException)
            {
                return NotFound("Order not found or expired");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Cancels a pending order and releases any locked seats
        /// </summary>
        /// <param name="orderToken">The unique identifier for the order</param>
        /// <returns>Success message if the order was cancelled</returns>
        /// <response code="200">Order successfully cancelled</response>
        /// <response code="404">If the order is not found or already expired</response>
        [HttpPost("orders/{orderToken}/cancel")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<string>> CancelOrder(Guid orderToken)
        {
            try
            {
                await _ticketService.CancelOrder(orderToken);
                return Ok("Order cancelled successfully");
            }
            catch (OrderNotFoundException)
            {
                return NotFound("Order not found or expired");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order");
                return StatusCode(500, "An error occurred while cancelling the order");
            }
        }

        [HttpGet("orders/{orderToken}/check")]
        [ProducesResponseType(typeof(OrderStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderStatusResponse>> CheckOrderStatus(Guid orderToken)
        {
            try
            {
                var order = await _context.TicketOrders
                    .FirstOrDefaultAsync(o => o.OrderToken == orderToken);
                    
                if (order == null)
                {
                    return NotFound("Order not found");
                }
                
                // If the order is expired according to our database timestamp
                if (order.ExpiresAt < DateTime.UtcNow || order.Status == OrderStatus.Expired || order.Status == OrderStatus.Cancelled)
                {
                    // Automatically expire the order if it hasn't been explicitly expired yet
                    if (order.Status == OrderStatus.Pending)
                    {
                        await _ticketService.CancelOrder(orderToken);
                        order.Status = OrderStatus.Expired;
                        await _context.SaveChangesAsync();
                    }
                    
                    return NotFound("Order expired");
                }
                
                // Order is valid
                return Ok(new OrderStatusResponse
                {
                    OrderToken = orderToken,
                    IsValid = true,
                    ExpiresAt = order.ExpiresAt,
                    Status = order.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking order status");
                return StatusCode(500, "An error occurred while checking order status");
            }
        }

        [HttpPost("orders/{orderToken}/expire")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<string>> ExpireOrder(Guid orderToken)
        {
            try
            {
                var order = await _context.TicketOrders
                    .FirstOrDefaultAsync(o => o.OrderToken == orderToken);
                    
                if (order == null)
                {
                    return NotFound("Order not found");
                }
                
                // Only expire pending orders
                if (order.Status == OrderStatus.Pending)
                {
                    await _ticketService.CancelOrder(orderToken);
                    order.Status = OrderStatus.Expired;
                    await _context.SaveChangesAsync();
                    return Ok("Order expired successfully");
                }
                
                return Ok("Order already processed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring order");
                return StatusCode(500, "An error occurred while expiring the order");
            }
        }

        /// <summary>
        /// Gets the ordered tickets in the form of a PDF file.
        /// </summary>
        /// <param name="orderToken">The unique identifier for the order</param>
        /// <returns>A PDF file with the tickets</returns>
        /// <response code="200">Tickets fetched and created successfully</response>
        /// <response code="404">If the order is not found or already expired</response>
        [HttpGet("{orderToken}/download")]
        [ProducesResponseType(typeof(File), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadPdfTickets(Guid orderToken)
        {
            try
            {
                var pdf = await _ticketService.GetTicketsByOrderToken(orderToken);
                return File(pdf, "application/pdf", orderToken + ".pdf");
            }
            catch (OrderNotFoundException)
            {
                return NotFound("Order not found or expired");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the tickets");
                return StatusCode(500, "An error occurred while creating the tickets");
            }
        }

        /// <summary>
        /// Gets the ordered tickets in the form of a PDF file.
        /// </summary>
        /// <param name="phoneBookingCode">The phone booking code for the tickets</param>
        /// <returns>A PDF file with the tickets</returns>
        /// <response code="200">Tickets fetched and created successfully</response>
        /// <response code="404">If the order is not found or already expired</response>
        [HttpGet("phone-booking/{phoneBookingCode}/download")]
        [ProducesResponseType(typeof(File), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadPdfTickets(string phoneBookingCode)
        {
            try
            {
                var pdf = await _ticketService.GetTicketsByPhoneBookingCode(phoneBookingCode);
                return File(pdf, "application/pdf", phoneBookingCode + ".pdf");
            }
            catch (OrderNotFoundException)
            {
                return NotFound("Order not found or expired");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the tickets");
                return StatusCode(500, "An error occurred while creating the tickets");
            }
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

                // Get the TicketOrder to set the TicketOrderId
                var order = await _context.TicketOrders
                    .FirstOrDefaultAsync(o => o.OrderToken == orderToken);
                
                if (order == null)
                {
                    _logger.LogError("Failed to find TicketOrder with token {OrderToken}", orderToken);
                    return false;
                }

                // Lock seats with orderToken
                var timestamp = DateTime.UtcNow;
                foreach (var seatId in seatIds)
                {
                    _context.SeatLocks.Add(new SeatLock
                    {
                        SeatId = seatId,
                        PresentationId = presentationId,
                        OrderToken = orderToken,
                        TicketOrderId = order.Id,
                        CreatedAt = timestamp,
                        ExpiresAt = timestamp.AddMinutes(10)
                    });
                }

                await _context.SaveChangesAsync();

                // Now update the seat availability
                foreach (var seatId in seatIds)
                {
                    var seat = await _context.Seats.FindAsync(seatId);
                    if (seat != null)
                    {
                        seat.IsAvailable = false; // Set the seat availability to false
                        await _context.SaveChangesAsync(); // Save changes to the database
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking seats");
                throw;
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
    }
} 