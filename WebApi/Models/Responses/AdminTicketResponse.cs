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
    }
} 