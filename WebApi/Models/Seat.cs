namespace WebApi.Models
{
    public class Seat
    {
        public Seat()
        {
            Tickets = new List<Ticket>();
        }

        public int Id { get; set; }
        public int HallId { get; set; }
        public int RowNumber { get; set; }
        public int SeatNumber { get; set; }
        public DateTime CreatedAt { get; set; }

        public required Hall Hall { get; set; }
        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}