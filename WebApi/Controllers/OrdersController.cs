using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Interfaces.Services;
using WebApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly IMailService _mailService;

        public OrdersController(
            ApplicationDbContext context, 
            ILogger<OrdersController> logger,
            IMailService mailService)
        {
            _context = context;
            _logger = logger;
            _mailService = mailService;
        }

        // GET: api/orders
        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            try
            {
                var orders = await _context.TicketOrders
                    .Include(o => o.Items)
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Presentation)
                            .ThenInclude(p => p.Movie)
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Presentation)
                            .ThenInclude(p => p.Hall)
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Seat)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                var result = orders.Select(o => new
                {
                    Id = o.Id,
                    OrderToken = o.OrderToken,
                    CreatedAt = o.CreatedAt,
                    CustomerName = GetCustomerNameFromTickets(o.Tickets),
                    CustomerEmail = GetCustomerEmailFromTickets(o.Tickets),
                    CustomerPhone = "",  // No phone field in model but added for UI
                    TicketCount = o.Tickets.Count,
                    Status = o.Status.ToString(),
                    PaymentMethod = "Credit Card",  // Default as we don't store this
                    PaymentId = o.Id.ToString(),  // Use order ID as payment ID
                    SubtotalAmount = o.Tickets.Sum(t => t.Presentation?.Price ?? 0),
                    DiscountAmount = 0,  // No discount field in model
                    TotalAmount = o.Tickets.Sum(t => t.Presentation?.Price ?? 0),
                    Tickets = o.Tickets.Select(t => new
                    {
                        Id = t.Id,
                        MovieTitle = t.Presentation?.Movie?.Title ?? "Unknown Movie",
                        ShowDateTime = t.Presentation?.StartTime ?? DateTime.MinValue,
                        HallName = t.Presentation?.Hall?.Name ?? "Unknown Hall",
                        SeatNumber = $"{t.Seat?.RowNumber}{t.Seat?.SeatNumber}" ?? "Unknown Seat",
                        Price = t.Presentation?.Price ?? 0,
                        Status = t.Status.ToString()
                    }).ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching orders");
                return StatusCode(500, "An error occurred while fetching orders");
            }
        }

        // GET: api/orders/recent
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentOrders()
        {
            try
            {
                var recentOrders = await _context.TicketOrders
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Presentation)
                            .ThenInclude(p => p.Movie)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)  // Only get 5 most recent orders
                    .ToListAsync();

                var result = recentOrders.Select(o => new
                {
                    Id = o.Id.ToString(),
                    Customer = GetCustomerNameFromTickets(o.Tickets),
                    Movie = o.Tickets.FirstOrDefault()?.Presentation?.Movie?.Title ?? "Multiple Movies",
                    Date = o.CreatedAt,
                    Amount = o.Tickets.Sum(t => t.Presentation?.Price ?? 0),
                    Status = o.Status.ToString()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recent orders");
                return StatusCode(500, "An error occurred while fetching recent orders");
            }
        }

        // GET: api/orders/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrder(int id)
        {
            try
            {
                var order = await _context.TicketOrders
                    .Include(o => o.Items)
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Presentation)
                            .ThenInclude(p => p.Movie)
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Presentation)
                            .ThenInclude(p => p.Hall)
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Seat)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return NotFound($"Order with ID {id} not found");
                }

                var result = new
                {
                    Id = order.Id,
                    OrderToken = order.OrderToken,
                    CreatedAt = order.CreatedAt,
                    CustomerName = GetCustomerNameFromTickets(order.Tickets),
                    CustomerEmail = GetCustomerEmailFromTickets(order.Tickets),
                    CustomerPhone = "",  // No phone field in model but added for UI
                    TicketCount = order.Tickets.Count,
                    Status = order.Status.ToString(),
                    PaymentMethod = "Credit Card",  // Default as we don't store this
                    PaymentId = order.Id.ToString(),  // Use order ID as payment ID
                    SubtotalAmount = order.Tickets.Sum(t => t.Presentation?.Price ?? 0),
                    DiscountAmount = 0,  // No discount field in model
                    TotalAmount = order.Tickets.Sum(t => t.Presentation?.Price ?? 0),
                    Tickets = order.Tickets.Select(t => new
                    {
                        Id = t.Id,
                        MovieTitle = t.Presentation?.Movie?.Title ?? "Unknown Movie",
                        ShowDateTime = t.Presentation?.StartTime ?? DateTime.MinValue,
                        HallName = t.Presentation?.Hall?.Name ?? "Unknown Hall",
                        SeatNumber = $"{t.Seat?.RowNumber}{t.Seat?.SeatNumber}" ?? "Unknown Seat",
                        Price = t.Presentation?.Price ?? 0,
                        Status = t.Status.ToString()
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching order {OrderId}", id);
                return StatusCode(500, $"An error occurred while fetching order {id}");
            }
        }

        // POST: api/orders/{id}/resend-email
        [HttpPost("{id}/resend-email")]
        public async Task<IActionResult> ResendEmail(int id)
        {
            try
            {
                var order = await _context.TicketOrders
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Presentation)
                            .ThenInclude(p => p.Movie)
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Presentation)
                            .ThenInclude(p => p.Hall)
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Seat)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return NotFound($"Order with ID {id} not found");
                }

                if (string.IsNullOrEmpty(GetCustomerEmailFromTickets(order.Tickets)))
                {
                    return BadRequest("Order does not have a customer email address");
                }

                // Get email template - this would typically be loaded from a file
                // For demo, we'll use a simple HTML template
                string emailTemplate = GetOrderConfirmationEmailTemplate(order);

                // Send email
                bool emailSent = _mailService.SendEmail(
                    GetCustomerNameFromTickets(order.Tickets) ?? "Customer",
                    GetCustomerEmailFromTickets(order.Tickets),
                    $"Your Cinema Tickets - Order #{order.Id}",
                    emailTemplate,
                    null  // No attachments
                );

                if (emailSent)
                {
                    return Ok(new { message = $"Confirmation email resent to {GetCustomerEmailFromTickets(order.Tickets)}" });
                }
                else
                {
                    return StatusCode(500, "Failed to send email");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending email for order {OrderId}", id);
                return StatusCode(500, $"An error occurred while resending email for order {id}");
            }
        }

        // Helper method to get customer name from the tickets
        private string GetCustomerNameFromTickets(ICollection<Ticket> tickets)
        {
            return tickets.FirstOrDefault()?.CustomerName ?? "Guest";
        }

        // Helper method to get customer email from the tickets
        private string GetCustomerEmailFromTickets(ICollection<Ticket> tickets)
        {
            return tickets.FirstOrDefault()?.CustomerEmail ?? "";
        }

        // Helper method to generate a simple email template
        private string GetOrderConfirmationEmailTemplate(TicketOrder order)
        {
            string ticketRows = string.Join("", order.Tickets.Select(t => $@"
                <tr>
                    <td style='padding: 8px; border: 1px solid #ddd;'>{t.Presentation?.Movie?.Title ?? "Unknown Movie"}</td>
                    <td style='padding: 8px; border: 1px solid #ddd;'>{t.Presentation?.StartTime.ToString("g") ?? "Unknown Time"}</td>
                    <td style='padding: 8px; border: 1px solid #ddd;'>{t.Presentation?.Hall?.Name ?? "Unknown Hall"}</td>
                    <td style='padding: 8px; border: 1px solid #ddd;'>{t.Seat?.RowNumber}{t.Seat?.SeatNumber.ToString() ?? "Unknown Seat"}</td>
                    <td style='padding: 8px; border: 1px solid #ddd;'>€{(t.Presentation?.Price != null ? t.Presentation.Price.ToString("0.00") : "0.00")}</td>
                </tr>"));

            decimal totalAmount = order.Tickets.Sum(t => t.Presentation?.Price ?? 0);

            string customerName = GetCustomerNameFromTickets(order.Tickets);

            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Your Cinema Tickets</title>
                </head>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto;'>
                    <div style='background-color: #f8f9fa; padding: 20px; text-align: center;'>
                        <h1 style='color: #121a2b;'>Your Cinema Tickets</h1>
                        <p>Order #{order.Id}</p>
                    </div>
                    
                    <div style='padding: 20px;'>
                        <p>Dear {customerName ?? "Customer"},</p>
                        
                        <p>Thank you for your order! Below are your ticket details:</p>
                        
                        <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>
                            <thead>
                                <tr style='background-color: #f2f2f2;'>
                                    <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Movie</th>
                                    <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Date & Time</th>
                                    <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Hall</th>
                                    <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Seat</th>
                                    <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Price</th>
                                </tr>
                            </thead>
                            <tbody>
                                {ticketRows}
                            </tbody>
                            <tfoot>
                                <tr>
                                    <td colspan='4' style='padding: 8px; border: 1px solid #ddd; text-align: right;'><strong>Total</strong></td>
                                    <td style='padding: 8px; border: 1px solid #ddd;'>€{totalAmount.ToString("0.00")}</td>
                                </tr>
                            </tfoot>
                        </table>
                        
                        <p>Please arrive at least 15 minutes before the show time. Your tickets will be available at the cinema box office.</p>
                        
                        <p>Enjoy your movie!</p>
                        
                        <p>Best regards,<br>Cinemagia Team</p>
                    </div>
                    
                    <div style='background-color: #121a2b; color: white; padding: 20px; text-align: center;'>
                        <p>&copy; 2023 Cinemagia. All rights reserved.</p>
                    </div>
                </body>
                </html>
            ";
        }
    }
} 