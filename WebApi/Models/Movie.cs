namespace WebApi.Models
{
    public class Movie
    {
        public Movie()
        {
            Presentations = new List<Presentation>();
            Formats = new List<MovieFormat>();
        }

        public int Id { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string PosterUrl { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string AgeRating { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string TrailerUrl { get; set; } = string.Empty;
        public string BackdropUrl { get; set; } = string.Empty;

        public ICollection<Presentation> Presentations { get; set; }
        public ICollection<MovieFormat> Formats { get; set; }
        public ICollection<Review> Reviews { get; set; }
    }
}