using System.Collections.Generic;

namespace FrontEnd.Models
{
    public class MovieScheduleItem
    {
        public int MovieId { get; set; }
        public string Title { get; set; }
        public int Duration { get; set; }
        public List<string> Formats { get; set; } = new List<string>();
        public string PosterUrl { get; set; }
        public List<ShowtimeItem> Showtimes { get; set; } = new List<ShowtimeItem>();
    }
} 