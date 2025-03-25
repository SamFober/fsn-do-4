using FrontEnd.Models;

namespace FrontEnd.Responses
{
    public class DailyMovieSchedule
    {
        public string Date { get; set; }
        public string DayOfWeek { get; set; }
        public List<ShowtimeItem> Showtimes { get; set; } = new List<ShowtimeItem>();
    }
}