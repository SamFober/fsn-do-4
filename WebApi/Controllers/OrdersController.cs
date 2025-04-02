using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

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
        private readonly ITicketPdfService _ticketPdfService;
        private readonly ITicketService _ticketService;

        public OrdersController(
            ApplicationDbContext context, 
            ILogger<OrdersController> logger,
            IMailService mailService,
            ITicketPdfService ticketPdfService,
            ITicketService ticketService)
        {
            _context = context;
            _logger = logger;
            _mailService = mailService;
            _ticketPdfService = ticketPdfService;
            _ticketService = ticketService;
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

                // Map entities to response models using the constructor
                var result = orders.Select(o => new AdminOrderResponse(o)).ToList();

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
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.Presentation)
                    .Include(o => o.Items)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)  // Only get 5 most recent orders
                    .ToListAsync();

                // Use AdminOrderResponse for consistency, then select only the needed properties
                var result = recentOrders
                    .Select(o => new AdminOrderResponse(o))
                    .Select(r => new {
                        Id = r.Id.ToString(),
                        Customer = r.CustomerName,
                        Movie = r.Tickets.FirstOrDefault()?.MovieTitle ?? "Multiple Movies",
                        Date = r.CreatedAt,
                        Amount = r.TotalAmount,
                        Status = r.Status
                    })
                    .ToList();

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

                // Use the constructor to map the entity to a response model
                var result = new AdminOrderResponse(order);

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
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return NotFound($"Order with ID {id} not found");
                }

                // Use the existing FinalizeOrder method which already handles 
                // getting tickets, creating PDFs, and sending emails
                await _ticketService.FinalizeOrder(order.OrderToken);

                return Ok(new { message = $"Confirmation email with tickets resent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending email for order {OrderId}", id);
                return StatusCode(500, $"An error occurred while resending email for order {id}: {ex.Message}");
            }
        }
    }
} 