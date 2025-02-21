using System;
using System.Collections.Generic;

namespace WebApi.Models
{
    public class TicketOrder
    {
        public int Id { get; set; }
        public Guid OrderToken { get; set; } // Unique token for frontend reference
        public int PresentationId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; } // Hold seats for X minutes
        public OrderStatus Status { get; set; }
        public virtual Presentation? Presentation { get; set; }
        public virtual List<TicketOrderItem> Items { get; set; } = new();
        public Dictionary<string, SeatingOption> AvailableOptions { get; set; } = new();
    }

    public class TicketOrderItem
    {
        public int Id { get; set; }
        public int TicketOrderId { get; set; }
        public int SeatId { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public TicketOrder Order { get; set; } = null!;
        public Seat Seat { get; set; } = null!;
    }

    public class SeatingOption
    {
        public string Type { get; set; } = string.Empty;
        public List<int> SeatIds { get; set; } = new();
        public DateTime ExpiresAt { get; set; }

        // Parameterless constructor for JSON deserialization
        public SeatingOption() { }

        public SeatingOption(string type, List<int> seatIds, DateTime expiresAt)
        {
            Type = type;
            SeatIds = seatIds;
            ExpiresAt = expiresAt;
        }
    }

    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Expired,
        Cancelled
    }
} 