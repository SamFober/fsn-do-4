using WebApi.Models.Responses;

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
        public int RequestedSeats { get; set; } // Number of seats originally requested
        public virtual Presentation? Presentation { get; set; }
        public Dictionary<string, SeatingOption> AvailableOptions { get; set; } = new();
        public ICollection<TicketOrderItem> Items { get; set; } = new List<TicketOrderItem>();
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

    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Expired,
        Cancelled
    }
}