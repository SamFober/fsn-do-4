using System;
using System.Collections.Generic;
using FrontEnd.Models;  // Add this to reference Hall and Movie

namespace FrontEnd.Models
{
    public class Presentation
    {
        public Presentation()
        {
            Tickets = new List<Ticket>();
            Rows = new List<Row>();
        }

        public int Id { get; set; }
        public int MovieId { get; set; }
        public int HallId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string Title { get; set; }
        public string HallName { get; set; }
        public List<Row> Rows { get; set; } = new List<Row>();
        public ICollection<Ticket> Tickets { get; set; }
    }
} 