using System;

namespace WebApi.Models.Responses
{
    public class AdminTicketResponse
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int MovieId { get; set; }
        public string MovieTitle { get; set; } = "";
        public DateTime ShowDateTime { get; set; }
        public string HallName { get; set; } = "";
        public string SeatNumber { get; set; } = "";
        public string Format { get; set; } = "";
        public decimal Price { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerEmail { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Default constructor
        public AdminTicketResponse() { }

        // Constructor that maps from entity to response
        public AdminTicketResponse(Ticket ticket, int orderId)
        {
            if (ticket == null)
                throw new ArgumentNullException(nameof(ticket));

            Id = ticket.Id;
            OrderId = orderId;
            MovieId = ticket.Presentation?.MovieId ?? 0;
            MovieTitle = ticket.Presentation?.Movie?.Title ?? "Unknown Movie";
            ShowDateTime = ticket.Presentation?.StartTime ?? DateTime.MinValue;
            HallName = ticket.Presentation?.Hall?.Name ?? "Unknown Hall";
            SeatNumber = $"{ticket.Seat?.RowNumber}{ticket.Seat?.SeatNumber}" ?? "Unknown Seat";
            Price = ticket.Presentation?.Price ?? 0;
            CustomerName = ticket.CustomerName;
            CustomerEmail = ticket.CustomerEmail;
            Status = ticket.Status.ToString();
            CreatedAt = ticket.CreatedAt;
            UpdatedAt = ticket.UpdatedAt;
        }
    }
} 