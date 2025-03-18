using System;
using System.Collections.Generic;
using WebApi.Models;

namespace WebApi.Models
{
    public class Movie
    {
        public Movie()
        {
            Presentations = new List<Presentation>();
        }

        public int Id { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string PosterUrl { get; set; }
        public string Genre { get; set; }
        public string AgeRating { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<Presentation> Presentations { get; set; }
    }
} 