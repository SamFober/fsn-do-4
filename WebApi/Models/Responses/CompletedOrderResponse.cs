namespace WebApi.Models
{
    public record CompletedOrderResponse
    {
        public required Guid orderToken { get; set; }
        public required string customerFirstName { get; set; }
        public required string customerLastName { get; set; }
        public required PaymentStatus paymentStatus { get; set; }
        public required string moviePosterUrl { get; set; }
    }
}
