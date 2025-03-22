using System;

namespace WebApi.Models
{
    public class SeatPresentation
    {
        public int Id { get; set; }
        public int SeatId { get; set; }
        public int PresentationId { get; set; }
        public bool IsAvailable { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Seat? Seat { get; set; }
        public Presentation? Presentation { get; set; }
    }
} 