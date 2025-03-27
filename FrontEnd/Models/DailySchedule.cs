using System.Collections.Generic;

namespace FrontEnd.Models
{
    public class DailySchedule
    {
        public string Date { get; set; }
        public string DayOfWeek { get; set; }
        public List<MovieScheduleItem> Movies { get; set; } = new List<MovieScheduleItem>();
    }
} 