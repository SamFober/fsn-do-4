using System;

namespace WebApi.Models.Responses
{
    public class PaymentResponse
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public decimal AmountPaid { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
    }
}
