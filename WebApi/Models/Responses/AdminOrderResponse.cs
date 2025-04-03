using System;
using System.Collections.Generic;
using System.Linq;

namespace WebApi.Models.Responses
{
    /// <summary>
    /// Detailed order response for admin endpoints
    /// </summary>
    public class AdminOrderResponse
    {
        public int Id { get; set; }
        public Guid OrderToken { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerEmail { get; set; } = "";
        public string CustomerPhone { get; set; } = ""; // Placeholder for potential future use
        public int TicketCount { get; set; }
        public string Status { get; set; } = "";
        public string PaymentMethod { get; set; } = "Credit Card"; // Default as we don't store this
        public string PaymentId { get; set; } = ""; // Usually the order ID
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; } // For potential future use
        public decimal TotalAmount { get; set; }
        public List<AdminTicketResponse> Tickets { get; set; } = new();

        // Default constructor
        public AdminOrderResponse() { }

        // Constructor that maps from entity to response
        public AdminOrderResponse(TicketOrder order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            Id = order.Id;
            OrderToken = order.OrderToken;
            CreatedAt = order.CreatedAt;
            CustomerName = GetCustomerName(order.Tickets);
            CustomerEmail = GetCustomerEmail(order.Tickets);
            CustomerPhone = ""; // No phone field in model but added for UI
            TicketCount = order.Tickets.Count;
            Status = order.Status.ToString();
            PaymentMethod = "Credit Card"; // Default as we don't store this
            PaymentId = order.Id.ToString(); // Use order ID as payment ID
            
            SubtotalAmount = order.Tickets.Sum(t => t.Presentation?.Price ?? 0);
            DiscountAmount = 0; // No discount field in model
            TotalAmount = order.Tickets.Sum(t => t.Presentation?.Price ?? 0);
            
            Tickets = order.Tickets
                .Select(t => new AdminTicketResponse(t, order.Id))
                .ToList();
        }

        // Helper method to get customer name from the tickets
        public static string GetCustomerName(ICollection<Ticket> tickets)
        {
            return tickets.FirstOrDefault()?.CustomerName ?? "Guest";
        }

        // Helper method to get customer email from the tickets
        public static string GetCustomerEmail(ICollection<Ticket> tickets)
        {
            return tickets.FirstOrDefault()?.CustomerEmail ?? "";
        }
    }
} 