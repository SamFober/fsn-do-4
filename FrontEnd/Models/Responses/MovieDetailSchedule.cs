namespace FrontEnd.Models.Responses
{
    public class MovieDetailSchedule
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int DurationMinutes { get; set; }
        public string Genre { get; set; }
        public string AgeRating { get; set; }
        public string PosterUrl { get; set; }
        public string BackdropUrl { get; set; }
        public List<FormatInfo> Formats { get; set; } = new List<FormatInfo>();
        public List<DailyMovieSchedule> Schedule { get; set; } = new List<DailyMovieSchedule>();
    }
}