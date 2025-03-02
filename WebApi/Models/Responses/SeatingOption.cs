using System;
using System.Collections.Generic;

namespace WebApi.Models.Responses
{
    public class SeatingOption
    {
        public SeatingOption(string description, List<int> seatIds, DateTime expiresAt, List<RowGroup>? groups = null)
        {
            Description = description;
            SeatIds = seatIds;
            ExpiresAt = expiresAt;
            Groups = groups ?? new List<RowGroup>();
        }

        public string Description { get; set; }
        public List<int> SeatIds { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<RowGroup> Groups { get; set; }
    }
} 