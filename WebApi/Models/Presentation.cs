using System;
using System.Collections.Generic;
using WebApi.Models;  // Add this to reference Hall and Movie

namespace WebApi.Models
{
    public class Presentation
    {
        public Presentation()
        {
            Tickets = new List<Ticket>();
        }

        public int Id { get; set; }
        public int MovieId { get; set; }
        public int HallId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public required Movie Movie { get; set; }
        public required Hall Hall { get; set; }
        public ICollection<Ticket> Tickets { get; set; }
    }
} 