namespace WebApi.Models
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
        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
        public ICollection<Presentation> Presentations { get; set; } = new List<Presentation>();
    }
}