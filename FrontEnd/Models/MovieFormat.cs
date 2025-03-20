namespace FrontEnd.Models
{
    public class MovieFormat
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        
        // Navigation property
        public int MovieId { get; set; }
        public Movie Movie { get; set; }
    }
} 