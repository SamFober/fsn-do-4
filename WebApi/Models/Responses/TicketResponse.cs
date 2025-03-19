namespace WebApi.Models.Responses
{
    public record TicketResponse(
        int TicketId,
        string MovieTitle,
        string HallName,
        DateTime StartTime,
        DateTime EndTime,
        int Row,
        int SeatNumber,
        string CustomerName,
        string CustomerEmail,
        TicketStatus Status,
        DateTime PurchaseDate
    );
}