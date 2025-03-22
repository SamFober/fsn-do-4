using System.Collections.Generic;

namespace FrontEnd.Models
{
    public class MovieScheduleResponse
    {
        public List<DailySchedule> Schedule { get; set; } = new List<DailySchedule>();
    }
} 