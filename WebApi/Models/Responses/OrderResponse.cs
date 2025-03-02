using System;
using System.Collections.Generic;

namespace WebApi.Models.Responses
{
    public class OrderResponse
    {
        public OrderResponse() { }

        public OrderResponse(Guid orderToken, List<int> seatIds)
        {
            OrderToken = orderToken;
            SeatIds = seatIds;
        }

        public virtual Guid OrderToken { get; set; }
        public virtual List<int> SeatIds { get; set; } = new();
    }
} 