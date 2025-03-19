using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontEnd.Models.Responses
{
    public class SingleOrderResponse : OrderResponse
    {
        public SingleOrderResponse() { }

        public SingleOrderResponse(String orderToken, int seatId) 
            : base(orderToken, new List<int> { seatId })
        {
        }

        public int SeatId => SeatIds.FirstOrDefault();
    }
} 