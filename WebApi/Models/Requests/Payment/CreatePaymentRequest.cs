namespace WebApi.Models.Requests.Payment
{
    public record CreatePaymentRequest
    {
        public required Guid orderToken { get; set; }
        public required string paymentMethod { get; set; }
    }
}
