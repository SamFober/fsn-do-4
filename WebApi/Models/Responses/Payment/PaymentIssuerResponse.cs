namespace WebApi.Models.Responses.Payment
{
    public record PaymentIssuerResponse
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string IssuerImage { get; set; }
    }
}
