namespace FrontEnd.Models.Dtos
{
    public class OrderDto
    {
        public Guid OrderId { get; set; } = Guid.Empty;
        public decimal TotalAmount { get; set; }
        public List<string> Discounts { get; set; } = new();

        // Voeg deze toe:
        public string MovieTitle { get; set; } = string.Empty;
        public List<string> Seats { get; set; } = new();
    }

}
