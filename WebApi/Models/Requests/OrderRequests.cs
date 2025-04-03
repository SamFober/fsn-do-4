namespace WebApi.Models.Requests
{
    public record StartOrderRequest(int PresentationId, bool isOnlineOrder = false);
    public record StartGroupOrderRequest(int PresentationId, int NumberOfSeats, bool isOnlineOrder = false);
    public record StartGroupOptionRequest(Guid OrderToken);
    public record AddSeatsRequest(List<int> SeatIds);
    public record ConfirmOrderRequest(string CustomerFirstName, string CustomerLastName, string CustomerEmail);
    public record AddConcessionRequest(int ConcessionItemId, int Quantity);
}