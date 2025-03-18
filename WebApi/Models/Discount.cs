using System;

namespace WebApi.Models
{
    public class Discount
    {
        public int Id { get; set; }
        public string Type { get; set; } // "Kinderkorting", "Studentenkorting", etc.
        public decimal Amount { get; set; }
        public bool RequiresValidation { get; set; } // Bijv. Studentenkaart vereist validatie.
        public bool OnlyWeekdays { get; set; } // Geldig van maandag t/m donderdag
        public bool ExcludesHolidays { get; set; } // Niet geldig op feestdagen/vakanties
    }
}
