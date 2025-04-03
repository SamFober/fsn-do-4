using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Responses.Payment;

namespace WebApi.Tests.Mocks.Services
{
    class PaymenServiceMock : IPaymentService
    {
        public Task<Payment> CreatePayment(Guid orderToken, Customer customer)
        {
            throw new NotImplementedException();
        }

        public Task<List<PaymentMethodResponse>> GetAvailablePaymentMethods()
        {
            throw new NotImplementedException();
        }

        public Task GetPayments()
        {
            throw new NotImplementedException();
        }

        public Task<bool> ProcessMolliePaymentUpdate(string molliePaymentId)
        {
            throw new NotImplementedException();
        }

        Task<TicketOrder> IPaymentService.ProcessMolliePaymentUpdate(string molliePaymentId)
        {
            throw new NotImplementedException();
        }
    }
}
