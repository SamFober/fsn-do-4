namespace WebApi.Models
{
    using System.ComponentModel.DataAnnotations;

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
        
        [StringLength(50)]
        public string Format { get; set; } = "Standard";
        
        [StringLength(100)]
        public string HallName { get; set; } = "";
        
        public int AvailableSeats { get; set; }
        
        public required Movie Movie { get; set; }
        public required Hall Hall { get; set; }
        public ICollection<Ticket> Tickets { get; set; }
    }
}