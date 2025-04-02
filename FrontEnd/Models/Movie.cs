namespace FrontEnd.Models
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
        public string PosterUrl { get; set; }
        public string Genre { get; set; }
        public string AgeRating { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string BackdropUrl { get; set; } = string.Empty;
        public string TrailerUrl { get; set; } = string.Empty;

        public ICollection<Presentation> Presentations { get; set; }
        public ICollection<MovieFormat> Formats { get; set; }
    }
}