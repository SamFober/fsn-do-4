namespace FrontEnd.Models.Requests
{
    public class PaymentRequest
    {
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public List<string> Discounts { get; set; } = new List<string>();
    }
}
