using System.Text.Json.Serialization;

namespace WebApi.Models.Responses
{
    public class GroupOrderResponse : OrderResponse
    {
        public GroupOrderResponse()
        {
            Seats = new List<SeatResponse>();
        }

        public GroupOrderResponse(Guid orderToken, List<int> seatIds, bool hasConsecutiveSeats = false)
            : base(orderToken, seatIds)
        {
            HasConsecutiveSeats = hasConsecutiveSeats;
            Seats = new List<SeatResponse>();
        }

        [JsonPropertyOrder(1)]
        public override Guid OrderToken { get; set; }

        [JsonPropertyOrder(2)]
        public override List<int> SeatIds { get; set; } = new();

        [JsonPropertyOrder(3)]
        public int TotalSeats => SeatIds.Count;

        [JsonPropertyOrder(4)]
        public bool HasConsecutiveSeats { get; set; }

        [JsonPropertyOrder(5)]
        public bool HasSplitOption => AvailableOptions.ContainsKey("split");

        [JsonPropertyOrder(6)]
        public Dictionary<string, SeatingOption> AvailableOptions { get; set; } = new();

        [JsonPropertyOrder(7)]
        public List<SeatResponse>? Seats { get; set; }
    }

    public class SeatResponse
    {
        public int Id { get; set; }
        public int RowNumber { get; set; }
        public int SeatNumber { get; set; }
    }
}