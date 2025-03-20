namespace WebApi.Models
{
    public class MovieFormat
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        
        // Navigation property
        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;
    }
} 