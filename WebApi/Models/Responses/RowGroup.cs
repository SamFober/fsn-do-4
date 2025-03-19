namespace WebApi.Models.Responses
{
    public record RowGroup(
        int RowNumber,
        List<int> SeatIds,
        string Description
    );
}