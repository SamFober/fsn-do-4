namespace FrontEnd.Models
{
    public class SecretPresentation
    {
        public int Id { get; set; }
        public string HallName { get; set; }
        public string Genre { get; set; }
        public string AgeRating { get; set; }
        public bool IsSecretMovie { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal Price { get; set; }
    }
}