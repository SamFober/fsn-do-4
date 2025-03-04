using System;
using System.Collections.Generic;
using FrontEnd.Models;

namespace FrontEnd.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public int PresentationId { get; set; }
        public int SeatId { get; set; }
        public required string CustomerName { get; set; }
        public required string CustomerEmail { get; set; }
        public DateTime PurchaseDate { get; set; }
        public TicketStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public required Presentation Presentation { get; set; }
        public required Seat Seat { get; set; }
    }

    public enum TicketStatus
    {
        Reserved,
        Paid,
        Cancelled
    }
} 