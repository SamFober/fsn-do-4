using System;
using System.Collections.Generic;
using WebApi.Models;

namespace WebApi.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public int PresentationId { get; set; }
        public int SeatId { get; set; }
        public int TicketOrderId { get; set; }
        public required string CustomerName { get; set; }
        public required string CustomerEmail { get; set; }
        public DateTime PurchaseDate { get; set; }
        public TicketStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public required Presentation Presentation { get; set; }
        public required Seat Seat { get; set; }
        public TicketOrder? TicketOrder { get; set; }
    }

    public enum TicketStatus
    {
        Reserved,
        Paid,
        Cancelled
    }
} 