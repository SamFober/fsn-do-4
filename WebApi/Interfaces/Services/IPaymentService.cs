using System;
using System.Threading.Tasks;
using WebApi.Models.Requests;
using WebApi.Models.Responses;

namespace WebApi.Interfaces.Services
{
    public interface IPaymentService
    {
        Task<PaymentResponse> ProcessPayment(PaymentRequest request);
    }
}
