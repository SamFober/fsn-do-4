using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Exceptions;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Requests;
using WebApi.Models.Responses;

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
        private readonly IMailService _mailService;

        public TicketsController(
            ITicketService ticketService,
            ApplicationDbContext context,
            ILogger<TicketsController> logger,
            IMailService mailService)
        {
            _ticketService = ticketService;
            _context = context;
            _logger = logger;
            _mailService = mailService;
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

                // Reset all seats to available first using the service
                var allSeatIds = presentation.Hall.Seats.Select(s => s.Id).ToList();
                await _ticketService.UpdateSeatAvailability(allSeatIds, true, presentationId);

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

                // Mark seats as unavailable using the service
                var seatIds = seatsToBook.Select(s => s.Id).ToList();
                await _ticketService.UpdateSeatAvailability(seatIds, false, presentationId);

                // Update the presentation's AvailableSeats to match
                if (presentation != null && presentation.Hall != null)
                {
                    int totalSeats = presentation.Hall.Rows * presentation.Hall.SeatsPerRow;
                    int bookedSeats = seatsToBook.Count;
                    presentation.AvailableSeats = totalSeats - bookedSeats;
                    await _context.SaveChangesAsync();
                }

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

        [HttpPost("test/reset-seats")]
        public async Task<ActionResult> ResetSeatAvailability(int presentationId)
        {
            try
            {
                // Get the presentation
                var presentation = await _context.Presentations
                    .Include(p => p.Hall)
                    .FirstOrDefaultAsync(p => p.Id == presentationId);

                if (presentation == null || presentation.Hall == null)
                {
                    return NotFound($"Presentation {presentationId} not found");
                }

                // Get all seats for this hall
                var seats = await _context.Seats
                    .Where(s => s.HallId == presentation.HallId)
                    .ToListAsync();

                var seatIds = seats.Select(s => s.Id).ToList();

                // Call the service to mark all seats as available
                await _ticketService.UpdateSeatAvailability(seatIds, true, presentationId);

                // Also remove any existing seat locks
                var existingLocks = await _context.SeatLocks
                    .Where(l => l.PresentationId == presentationId)
                    .ToListAsync();

                if (existingLocks.Any())
                {
                    _context.SeatLocks.RemoveRange(existingLocks);
                    await _context.SaveChangesAsync();
                }

                return Ok($"Reset {seatIds.Count} seats to available for presentation {presentationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting seat availability for presentation {PresentationId}", presentationId);
                return StatusCode(500, "An error occurred while resetting seat availability");
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
                _logger.LogInformation("StartGroupOrder called with PresentationId: {PresentationId}, NumberOfSeats: {NumberOfSeats}",
                    request.PresentationId, request.NumberOfSeats);

                if (request.PresentationId <= 0)
                {
                    _logger.LogWarning("Invalid presentation ID: {PresentationId}", request.PresentationId);
                    return NotFound("Presentation not found");
                }

                if (request.NumberOfSeats <= 0 || request.NumberOfSeats > 20)
                {
                    _logger.LogWarning("Invalid number of seats requested: {NumberOfSeats}", request.NumberOfSeats);
                    return BadRequest("Invalid number of seats requested");
                }

                var response = await _ticketService.StartGroupOrder(request);
                if (response == null)
                {
                    _logger.LogWarning("StartGroupOrder returned null response for presentation {PresentationId}", request.PresentationId);
                    return BadRequest("Failed to create order");
                }

                _logger.LogInformation("Successfully created group order with token {OrderToken} for presentation {PresentationId}",
                    response.OrderToken, request.PresentationId);
                return Ok(response);
            }
            catch (NoSeatsAvailableException)
            {
                _logger.LogWarning("No seats available for presentation {PresentationId}", request.PresentationId);
                return BadRequest("No seats available");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting group order for presentation {PresentationId}, seats: {NumberOfSeats}: {ErrorMessage}",
                    request.PresentationId, request.NumberOfSeats, ex.Message);
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

                    // Make seats available again using the service
                    await _ticketService.UpdateSeatAvailability(seatIdsToMakeAvailable, true, order.PresentationId);
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
        [ProducesResponseType(typeof(List<WebApi.Models.Responses.TicketResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<WebApi.Models.Responses.TicketResponse>>> ConfirmOrder(
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
        /// Generates the comfirmation email and sends it to the user
        /// </summary>
        /// <param name="orderToken">The unique identifier for the order</param>
        /// <returns>A status code</returns>
        /// <response code="200">Order finalized</response>
        /// <response code="404">If the order is not found or already expired</response>
        [HttpGet("{orderToken}/finalize")]
        [ProducesResponseType(typeof(IActionResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> FinalizeOrder(Guid orderToken)
        {
            try
            {
                await _ticketService.FinalizeOrder(orderToken);
                return Ok();
            } 
            catch(OrderNotFoundException ex)
            {
                _logger.LogWarning(ex, "Order not found");
                return NotFound("Order not found");
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to finalize order");
                return StatusCode(500, "An error occurred while finalizing the order");
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

                Response.Headers.Add("Content-Disposition", $"inline; filename=\"ticket-{orderToken}.pdf\"");
                Response.Headers.Add("Cache-Control", "public, max-age=60");
                
                return File(pdf, "application/pdf");
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
                
                Response.Headers.Add("Content-Disposition", $"inline; filename=\"ticket-{phoneBookingCode}.pdf\"");
                Response.Headers.Add("Cache-Control", "public, max-age=60");
                
                return File(pdf, "application/pdf");
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

        [HttpGet]
        [ProducesResponseType(typeof(List<WebApi.Models.Responses.TicketResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllTickets()
        {
            try
            {
                // Fetch all tickets with related data
                var tickets = await _context.Tickets
                    .Include(t => t.Presentation)
                        .ThenInclude(p => p.Movie)
                    .Include(t => t.Presentation)
                        .ThenInclude(p => p.Hall)
                    .Include(t => t.Seat)
                    .Include(t => t.TicketOrder)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                // Map to response objects using the proper TicketResponse record
                var response = tickets.Select(ticket => new WebApi.Models.Responses.TicketResponse(
                    TicketId: ticket.Id,
                    MovieTitle: ticket.Presentation?.Movie?.Title ?? "Unknown Movie",
                    HallName: ticket.Presentation?.Hall?.Name ?? "Unknown Hall",
                    StartTime: ticket.Presentation?.StartTime ?? DateTime.MinValue,
                    EndTime: ticket.Presentation?.EndTime ?? DateTime.MinValue,
                    Row: ticket.Seat?.RowNumber ?? 0,
                    SeatNumber: ticket.Seat?.SeatNumber ?? 0,
                    CustomerName: ticket.CustomerName ?? "Unknown",
                    CustomerEmail: ticket.CustomerEmail ?? "",
                    Status: ticket.Status,
                    PurchaseDate: ticket.PurchaseDate
                )).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all tickets");
                return StatusCode(500, "An error occurred while retrieving tickets");
            }
        }

        [HttpGet("admin")]
        [ProducesResponseType(typeof(List<AdminTicketResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAdminTickets()
        {
            try
            {
                // Fetch all tickets with related data
                var tickets = await _context.Tickets
                    .Include(t => t.Presentation)
                        .ThenInclude(p => p.Movie)
                    .Include(t => t.Presentation)
                        .ThenInclude(p => p.Hall)
                    .Include(t => t.Seat)
                    .Include(t => t.TicketOrder)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                // Map to response objects using the AdminTicketResponse class for admin dashboard
                var response = tickets.Select(ticket => new AdminTicketResponse
                {
                    Id = ticket.Id,
                    OrderId = ticket.TicketOrder?.Id ?? 0,
                    MovieId = ticket.Presentation?.MovieId ?? 0,
                    MovieTitle = ticket.Presentation?.Movie?.Title ?? "Unknown Movie",
                    ShowDateTime = ticket.Presentation?.StartTime ?? DateTime.MinValue,
                    HallName = ticket.Presentation?.Hall?.Name ?? "Unknown Hall",
                    SeatNumber = $"{ticket.Seat?.RowNumber}{ticket.Seat?.SeatNumber}",
                    Format = ticket.Presentation?.Format ?? "",
                    Price = ticket.Presentation?.Price ?? 0,
                    CustomerName = ticket.CustomerName ?? "",
                    CustomerEmail = ticket.CustomerEmail ?? "",
                    CustomerPhone = "", // This field doesn't exist in the model
                    Status = ticket.Status.ToString(),
                    CreatedAt = ticket.CreatedAt,
                    UpdatedAt = ticket.UpdatedAt
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving admin tickets");
                return StatusCode(500, "An error occurred while retrieving tickets");
            }
        }

        // New endpoint for individual ticket cancellation (used by admin)
        [HttpPost("{ticketId}/cancel")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelTicket(int ticketId, [FromBody] CancelTicketRequest request)
        {
            try
            {
                var ticket = await _context.Tickets
                    .Include(t => t.Presentation)
                    .Include(t => t.Seat)
                    .Include(t => t.TicketOrder)
                    .FirstOrDefaultAsync(t => t.Id == ticketId);

                if (ticket == null)
                {
                    return NotFound($"Ticket with ID {ticketId} not found");
                }

                // Check if the ticket is already cancelled
                if (ticket.Status == TicketStatus.Cancelled)
                {
                    return BadRequest("Ticket is already cancelled");
                }

                // Update ticket status
                ticket.Status = TicketStatus.Cancelled;
                ticket.UpdatedAt = DateTime.UtcNow;

                // Make the seat available again for this presentation
                var seatPresentation = await _context.SeatPresentations
                    .FirstOrDefaultAsync(sp => sp.PresentationId == ticket.PresentationId && sp.SeatId == ticket.SeatId);

                if (seatPresentation != null)
                {
                    seatPresentation.IsAvailable = true;
                    seatPresentation.UpdatedAt = DateTime.UtcNow;
                }

                // Update presentation available seats count
                var presentation = ticket.Presentation;
                if (presentation != null)
                {
                    presentation.AvailableSeats = Math.Min(
                        presentation.AvailableSeats + 1,
                        presentation.Hall?.Rows * presentation.Hall?.SeatsPerRow ?? presentation.AvailableSeats + 1
                    );
                }

                await _context.SaveChangesAsync();

                // Record cancellation reason and other details
                _logger.LogInformation(
                    "Ticket {TicketId} cancelled. Reason: {Reason}, Refund issued: {Refund}",
                    ticketId,
                    request.Reason,
                    request.IssueRefund
                );

                // If refund is requested, log it (in a real system, we would process the refund)
                if (request.IssueRefund)
                {
                    _logger.LogInformation(
                        "Refund issued for ticket {TicketId}, amount: {Amount}",
                        ticketId,
                        presentation?.Price ?? 0
                    );
                }

                // Notify customer if we have their email (optional)
                if (!string.IsNullOrEmpty(ticket.CustomerEmail))
                {
                    try
                    {
                        // Simple email notification
                        string emailBody = $@"
                            <h1>Ticket Cancellation</h1>
                            <p>Dear {ticket.CustomerName},</p>
                            <p>Your ticket for {ticket.Presentation?.Movie?.Title} on {ticket.Presentation?.StartTime:g} has been cancelled.</p>
                            <p>Reason: {request.Reason}</p>
                            " + (request.IssueRefund ? $"<p>A refund of €{ticket.Presentation?.Price:0.00} has been issued.</p>" : "");

                        _mailService.SendEmail(
                            ticket.CustomerName,
                            ticket.CustomerEmail,
                            "Your Cinema Ticket has been Cancelled",
                            emailBody,
                            null
                        );
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail if email sending fails
                        _logger.LogWarning(ex, "Failed to send cancellation email to {Email}", ticket.CustomerEmail);
                    }
                }

                return Ok(new { 
                    message = "Ticket cancelled successfully",
                    refundIssued = request.IssueRefund,
                    refundAmount = request.IssueRefund ? ticket.Presentation?.Price : 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling ticket {TicketId}", ticketId);
                return StatusCode(500, "An error occurred while cancelling the ticket");
            }
        }

        [HttpGet("test/almost-sold-out-test")]
        public async Task<ActionResult> SetupAlmostSoldOutTest()
        {
            try
            {
                // First, let's get a movie to create presentations for
                var movie = await _context.Movies
                    .FirstOrDefaultAsync(m => m.IsActive);

                if (movie == null)
                {
                    return NotFound("No active movies found in the database");
                }

                // Get halls to use for our test presentations
                var halls = await _context.Halls.Take(3).ToListAsync();
                if (halls.Count < 1)
                {
                    return NotFound("No halls found in the database");
                }

                // Create presentations for the next 7 days instead of just today
                var startDate = DateTime.Today;
                var endDate = startDate.AddDays(6); // 7 days total (today + 6 more days)

                // Get the current time to ensure we don't create presentations in the past for today
                var currentTime = DateTime.Now.TimeOfDay;

                var allResults = new List<object>();
                var allCreatedOrUpdated = new List<Presentation>();

                // Iterate through each day in our date range
                for (var testDate = startDate; testDate <= endDate; testDate = testDate.AddDays(1))
                {
                    var isToday = testDate.Date == DateTime.Today;

                    // Create startTimes - for today, ensure they're in the future
                    // For other days, use consistent times
                    var startTimes = new List<TimeSpan>();

                    // Base times that will be used for all days
                    var baseTimes = new List<TimeSpan>
                    {
                        new TimeSpan(10, 0, 0),   // 10:00 AM
                        new TimeSpan(14, 30, 0),  // 2:30 PM
                        new TimeSpan(17, 0, 0),   // 5:00 PM
                        new TimeSpan(19, 30, 0),  // 7:30 PM
                        new TimeSpan(21, 0, 0),   // 9:00 PM
                        new TimeSpan(22, 15, 0)   // 10:15 PM
                    };

                    // For today, filter out times that have already passed
                    if (isToday)
                    {
                        startTimes = baseTimes.Where(t => t > currentTime.Add(new TimeSpan(0, 30, 0))).ToList();

                        // If all times have passed for today, add some times for late evening
                        if (!startTimes.Any())
                        {
                            // Add a couple of late evening times if it's still early enough
                            var currentHour = currentTime.Hours;
                            if (currentHour < 22)
                            {
                                startTimes.Add(new TimeSpan(22, 0, 0));  // 10:00 PM
                                startTimes.Add(new TimeSpan(23, 30, 0));  // 11:30 PM
                            }
                            else if (currentHour < 23)
                            {
                                startTimes.Add(new TimeSpan(23, 30, 0));  // 11:30 PM
                            }
                        }
                    }
                    else
                    {
                        // For future days, use all base times
                        startTimes = baseTimes;
                    }

                    // Skip this day if we don't have any valid start times
                    if (!startTimes.Any())
                    {
                        continue;
                    }

                    var results = new List<object>();
                    var createdOrUpdated = new List<Presentation>();

                    // For each hall, set up presentations with different levels of availability
                    foreach (var hall in halls)
                    {
                        // Get total seats in this hall
                        int totalSeats = hall.Rows * hall.SeatsPerRow;

                        // Create availability scenarios:
                        // 1. Completely sold out (0% seats remaining)
                        // 2. Almost sold out (less than 15% seats remaining)
                        // 3. Moderately full (30-50% seats remaining)
                        // 4. Plenty available (70-90% seats remaining)
                        var availabilityScenarios = new[] {
                            0,                          // 0% available (completely sold out)
                            (int)(totalSeats * 0.05),   // 5% available (almost sold out)
                            (int)(totalSeats * 0.35),   // 35% available (moderately full)
                            (int)(totalSeats * 0.8)     // 80% available (plenty available)
                        };

                        // Limit how many scenarios we use based on available time slots
                        int maxScenarios = Math.Min(startTimes.Count, availabilityScenarios.Length);

                        for (int i = 0; i < maxScenarios; i++)
                        {
                            var startTime = startTimes[i];
                            var availableSeats = availabilityScenarios[i];

                            // Create a start DateTime and calculate end time based on movie duration
                            var presentationStart = testDate.Add(startTime);
                            var presentationEnd = presentationStart.AddMinutes(movie.DurationMinutes);

                            // Create or update a test presentation
                            var presentation = await _context.Presentations
                                .FirstOrDefaultAsync(p =>
                                    p.MovieId == movie.Id &&
                                    p.HallId == hall.Id &&
                                    p.StartTime.Date == testDate.Date &&
                                    p.StartTime.Hour == presentationStart.Hour);

                            if (presentation == null)
                            {
                                // Create new presentation
                                presentation = new Presentation
                                {
                                    MovieId = movie.Id,
                                    Movie = movie,
                                    HallId = hall.Id,
                                    Hall = hall,
                                    StartTime = presentationStart,
                                    EndTime = presentationEnd,
                                    Price = 12.50m,
                                    Format = i == 0 ? "2D" : (i == 1 ? "3D" : "IMAX"),
                                    HallName = hall.Name,
                                    AvailableSeats = availableSeats,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };

                                _context.Presentations.Add(presentation);
                            }
                            else
                            {
                                // Update existing presentation
                                presentation.AvailableSeats = availableSeats;
                                presentation.UpdatedAt = DateTime.UtcNow;
                            }

                            await _context.SaveChangesAsync();
                            createdOrUpdated.Add(presentation);

                            // Create or update seat presentations for this presentation
                            await EnsureSeatPresentationsExist(presentation.Id, availableSeats, totalSeats);

                            // Add presentation with availability info to results
                            results.Add(new
                            {
                                PresentationId = presentation.Id,
                                Date = presentation.StartTime.ToShortDateString(),
                                HallName = hall.Name,
                                MovieTitle = movie.Title,
                                StartTime = presentation.StartTime.ToString("HH:mm"),
                                Format = presentation.Format,
                                TotalSeats = totalSeats,
                                AvailableSeats = availableSeats,
                                SoldOutPercentage = 100 - (availableSeats * 100 / totalSeats),
                                AlmostSoldOut = availableSeats > 0 && availableSeats <= totalSeats * 0.15,
                                SoldOut = availableSeats == 0
                            });
                        }
                    }

                    // Add this day's results to the overall results
                    allResults.AddRange(results);
                    allCreatedOrUpdated.AddRange(createdOrUpdated);
                }

                // Group results by date for better presentation
                var groupedResults = allResults
                    .GroupBy(r => ((dynamic)r).Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Presentations = g.ToList()
                    })
                    .OrderBy(g => DateTime.Parse(g.Date))
                    .ToList();

                return Ok(new
                {
                    DateRange = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                    PresentationsCreated = allCreatedOrUpdated.Count,
                    Note = "These presentations are available in the regular schedule. Refresh the schedule page to view them.",
                    ScheduleByDay = groupedResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up almost sold out test");
                return StatusCode(500, $"Error setting up test: {ex.Message}");
            }
        }

        // Helper method to create or update seat presentations for a given presentation
        private async Task EnsureSeatPresentationsExist(int presentationId, int availableSeats, int totalSeats)
        {
            // Get seats for this presentation's hall
            var presentation = await _context.Presentations
                .Include(p => p.Hall)
                .ThenInclude(h => h.Seats)
                .FirstOrDefaultAsync(p => p.Id == presentationId);

            if (presentation == null || presentation.Hall == null || !presentation.Hall.Seats.Any())
                return;

            // Get all the seats
            var seats = presentation.Hall.Seats.ToList();
            if (!seats.Any())
                return;

            // Calculate how many seats should be unavailable
            int unavailableCount = totalSeats - availableSeats;

            // Get existing seat presentations
            var existingSeatPresentations = await _context.SeatPresentations
                .Where(sp => sp.PresentationId == presentationId)
                .ToListAsync();

            // If no existing seat presentations, create them
            if (!existingSeatPresentations.Any())
            {
                _logger.LogInformation($"Creating {seats.Count} new seat presentations for presentation {presentationId}");

                // Shuffle the seats to randomize which ones are available
                var random = new Random(presentationId); // Use presentation ID as seed for consistent results
                var shuffledSeats = seats.OrderBy(s => random.Next()).ToList();

                // Create seat presentations for all seats
                var seatPresentations = new List<SeatPresentation>();

                for (int i = 0; i < shuffledSeats.Count; i++)
                {
                    var seat = shuffledSeats[i];
                    var isAvailable = i >= unavailableCount; // First 'unavailableCount' seats are not available

                    seatPresentations.Add(new SeatPresentation
                    {
                        SeatId = seat.Id,
                        PresentationId = presentationId,
                        IsAvailable = isAvailable,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                await _context.SeatPresentations.AddRangeAsync(seatPresentations);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created {seatPresentations.Count} seat presentations with {availableSeats} available seats");

                // Create actual ticket orders and tickets for unavailable seats
                await CreateFakeTicketOrders(shuffledSeats.Take(unavailableCount).Select(s => s.Id).ToList(), presentationId);
            }
            else
            {
                _logger.LogInformation($"Updating {existingSeatPresentations.Count} existing seat presentations for presentation {presentationId}");

                // Update existing seat presentations
                // Make sure we have a record for every seat
                var existingSeatIds = existingSeatPresentations.Select(sp => sp.SeatId).ToHashSet();
                var missingSeatPresentations = new List<SeatPresentation>();

                foreach (var seat in seats)
                {
                    if (!existingSeatIds.Contains(seat.Id))
                    {
                        missingSeatPresentations.Add(new SeatPresentation
                        {
                            SeatId = seat.Id,
                            PresentationId = presentationId,
                            IsAvailable = true, // Default to available
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                if (missingSeatPresentations.Any())
                {
                    _logger.LogInformation($"Adding {missingSeatPresentations.Count} missing seat presentations");
                    await _context.SeatPresentations.AddRangeAsync(missingSeatPresentations);
                    await _context.SaveChangesAsync();

                    // Refresh the list of all seat presentations
                    existingSeatPresentations = await _context.SeatPresentations
                        .Where(sp => sp.PresentationId == presentationId)
                        .ToListAsync();
                }

                // Shuffle for random availability - use a combination of presentation ID and current time
                // for a different result each time
                var random = new Random(presentationId + (int)DateTime.Now.Ticks % 10000);
                var shuffledPresentations = existingSeatPresentations.OrderBy(s => random.Next()).ToList();

                // Get the unavailable seat IDs
                var unavailableSeatIds = new List<int>();

                // Update availability based on our desired number of available seats
                for (int i = 0; i < shuffledPresentations.Count; i++)
                {
                    var isAvailable = i >= unavailableCount;
                    if (shuffledPresentations[i].IsAvailable != isAvailable)
                    {
                        shuffledPresentations[i].IsAvailable = isAvailable;
                        shuffledPresentations[i].UpdatedAt = DateTime.UtcNow;

                        if (!isAvailable)
                        {
                            unavailableSeatIds.Add(shuffledPresentations[i].SeatId);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Updated seat presentations to have {availableSeats} available seats");

                // Create tickets for the newly unavailable seats
                if (unavailableSeatIds.Any())
                {
                    // First, remove any existing tickets or locks for these seats to avoid conflicts
                    var existingTickets = await _context.Tickets
                        .Where(t => t.PresentationId == presentationId && unavailableSeatIds.Contains(t.SeatId))
                        .ToListAsync();

                    if (existingTickets.Any())
                    {
                        _context.Tickets.RemoveRange(existingTickets);
                        await _context.SaveChangesAsync();
                    }

                    var existingLocks = await _context.SeatLocks
                        .Where(l => l.PresentationId == presentationId && unavailableSeatIds.Contains(l.SeatId))
                        .ToListAsync();

                    if (existingLocks.Any())
                    {
                        _context.SeatLocks.RemoveRange(existingLocks);
                        await _context.SaveChangesAsync();
                    }

                    // Now create new ticket orders and tickets
                    await CreateFakeTicketOrders(unavailableSeatIds, presentationId);
                }
            }
        }

        // Helper method to create fake ticket orders and tickets
        private async Task CreateFakeTicketOrders(List<int> seatIds, int presentationId)
        {
            if (!seatIds.Any())
                return;

            _logger.LogInformation($"Creating fake ticket orders for {seatIds.Count} seats in presentation {presentationId}");

            // Create tickets in batches of 1-4 seats per order
            var random = new Random(presentationId);
            var remainingSeats = new List<int>(seatIds);

            while (remainingSeats.Any())
            {
                // Determine batch size (1-4 seats per order)
                int batchSize = Math.Min(random.Next(1, 5), remainingSeats.Count);
                var batchSeatIds = remainingSeats.Take(batchSize).ToList();
                remainingSeats.RemoveRange(0, batchSize);

                // Create a new ticket order
                var orderToken = Guid.NewGuid();
                var orderTimestamp = DateTime.UtcNow.AddMinutes(-random.Next(5, 60)); // Random time in the past

                // Get the presentation
                var presentation = await _context.Presentations.FindAsync(presentationId);
                if (presentation == null)
                {
                    _logger.LogError($"Presentation {presentationId} not found when creating fake orders");
                    continue;
                }

                var order = new TicketOrder
                {
                    PresentationId = presentationId,
                    OrderToken = orderToken,
                    Status = OrderStatus.Confirmed, // Order is confirmed
                    CreatedAt = orderTimestamp,
                    ExpiresAt = orderTimestamp.AddMinutes(10), // Already expired
                    Items = new List<TicketOrderItem>()
                };

                _context.TicketOrders.Add(order);
                await _context.SaveChangesAsync(); // Save to get the order ID

                // Create order items
                foreach (var seatId in batchSeatIds)
                {
                    order.Items.Add(new TicketOrderItem
                    {
                        TicketOrderId = order.Id,
                        SeatId = seatId,
                        CreatedAt = orderTimestamp
                    });
                }

                await _context.SaveChangesAsync();

                // Create tickets
                var tickets = new List<Ticket>();
                var customerName = $"Test Customer {random.Next(1000, 9999)}";
                var customerEmail = $"test{random.Next(1000, 9999)}@example.com";

                foreach (var seatId in batchSeatIds)
                {
                    var seat = await _context.Seats.FindAsync(seatId);

                    if (seat != null)
                    {
                        // Create a new ticket with all required properties
                        var ticket = new Ticket
                        {
                            PresentationId = presentationId,
                            SeatId = seatId,
                            TicketOrderId = order.Id,
                            CustomerName = customerName,
                            CustomerEmail = customerEmail,
                            Status = TicketStatus.Reserved,
                            PurchaseDate = orderTimestamp,
                            CreatedAt = orderTimestamp,
                            UpdatedAt = orderTimestamp,
                            Presentation = presentation,
                            Seat = seat,
                            TicketOrder = order
                        };

                        tickets.Add(ticket);
                    }
                }

                if (tickets.Any())
                {
                    await _context.Tickets.AddRangeAsync(tickets);
                    await _context.SaveChangesAsync();
                }
            }

            _logger.LogInformation($"Successfully created fake ticket orders for presentation {presentationId}");
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

                // Use the ticket service to update seat availability 
                // This will also update the presentation's AvailableSeats count
                await _ticketService.UpdateSeatAvailability(seatIds, false, presentationId);

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