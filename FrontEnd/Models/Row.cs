namespace FrontEnd.Models
{
    public class Row
    {
        public int RowNumber { get; set; }
        public List<Seat> Seats { get; set; } = new List<Seat>();
    }
}