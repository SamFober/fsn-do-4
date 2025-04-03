using WebApi.Models;
using WebApi.Models.Responses.Payment;

namespace WebApi.Interfaces.Services
{
    public interface IPaymentService
    {
        Task GetPayments();
        Task<Payment> CreatePayment(Guid orderToken, Customer customer);
        Task<TicketOrder> ProcessMolliePaymentUpdate(string molliePaymentId);
        Task<List<PaymentMethodResponse>> GetAvailablePaymentMethods();
    }
}
