namespace FrontEnd.Models.Responses
{
    public class OrderResponse
    {
        public OrderResponse() { }

        public OrderResponse(string orderToken, List<int> seatIds, int? preselectedSeatId = null)
        {
            OrderToken = orderToken;
            SeatIds = seatIds;
            PreselectedSeatId = preselectedSeatId;
        }

        public virtual String OrderToken { get; set; }
        public virtual List<int> SeatIds { get; set; } = new();
        public virtual int? PreselectedSeatId { get; set; }
    }
}