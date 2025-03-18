using System;
using System.Collections.Generic;
using FrontEnd.Models;

namespace FrontEnd.Models
{
    public class Hall
    {
        public Hall()
        {
            Seats = new List<Seat>();
            Presentations = new List<Presentation>();
        }

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Rows { get; set; }
        public int SeatsPerRow { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
        public ICollection<Presentation> Presentations { get; set; } = new List<Presentation>();
    }
} 