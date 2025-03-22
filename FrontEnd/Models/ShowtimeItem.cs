namespace FrontEnd.Models
{
    public class ShowtimeItem
    {
        public int Id { get; set; }
        public int HallId { get; set; }
        public string HallName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Format { get; set; }
        public decimal Price { get; set; }
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
        public bool IsAlmostFull { get; set; }
        public bool IsSoldOut { get; set; }
    }
} 