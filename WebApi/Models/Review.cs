namespace WebApi.Models
{
    public class Review
    {
        public int Id { get; set; }
        public int MovieId { get; set; } // Foreign key to movie
        public string AuthorName { get; set; }
        public string Email { get; set; }
        public int Rating { get; set; } //1-5 stars
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation property
        public Movie? Movie { get; set; }
    }
}
