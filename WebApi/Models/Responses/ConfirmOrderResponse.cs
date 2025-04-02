namespace WebApi.Models.Responses
{
    public record ConfirmOrderResponse
    {
        public required Guid orderToken { get; set; }
        public string? checkoutUrl { get; set; }
    }
}
