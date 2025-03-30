namespace WebApi.Models.Requests
{
    public record StartOrderRequest(int PresentationId);
    public record StartGroupOrderRequest(int PresentationId, int NumberOfSeats);
    public record StartGroupOptionRequest(Guid OrderToken);
    public record AddSeatsRequest(List<int> SeatIds);
    public record ConfirmOrderRequest(string CustomerName, string CustomerEmail);
    public record AddConcessionRequest(int ConcessionItemId, int Quantity);
}