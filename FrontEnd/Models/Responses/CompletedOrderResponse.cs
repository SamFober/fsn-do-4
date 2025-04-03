namespace FrontEnd.Models.Responses
{
    public record CompletedOrderResponse
    {
        public required string customerFirstName { get; set; }
        public required string customerLastName { get; set; }
        public required PaymentStatus paymentStatus { get; set; }
        public required string moviePosterUrl { get; set; }
    }

    public enum PaymentStatus
    {
        Pending,
        Expired,
        Canceled,
        Failed,
        Paid
    }
}
