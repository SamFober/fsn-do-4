namespace WebApi.Models.Responses.Payment
{
    public record PaymentMethodResponse
    {
        public required string Id { get; set; }
        public required string Image { get; set; }
        public required string Description { get; set; }
        public required List<PaymentIssuerResponse> PaymentIssuers { get; set; } = new List<PaymentIssuerResponse>();
    }
}
