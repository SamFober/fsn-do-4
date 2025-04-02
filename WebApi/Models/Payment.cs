using System.ComponentModel.DataAnnotations.Schema;

namespace WebApi.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public string? MolliePaymentId { get; set; }
        public required decimal Amount { get; set; }
        public required string Description { get; set; }
        public required PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        public required string CheckoutUrl { get; set; }

        public int? TicketOrderId { get; set; }
        public TicketOrder? TicketOrder { get; set; }
    }

    public enum PaymentStatus
    {
        Pending,
        Expired,
        Canceled,
        Failed,
        Paid
    }
}
