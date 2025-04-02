namespace WebApi.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string EmailAddress { get; set; }

        public int? TicketOrderId { get; set; }
        public TicketOrder? TicketOrder { get; set; }
    }
}
