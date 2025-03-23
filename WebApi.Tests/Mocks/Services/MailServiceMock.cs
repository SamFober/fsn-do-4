using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApi.Interfaces.Services;

namespace WebApi.Tests.Mocks.Services
{
    class MailServiceMock : IMailService
    {
        public async Task<bool> SendEmail(string recipient, string subject, string body, List<object>? attachments)
        {
            return true;
        }

        public async Task<string> TicketOrderCompleteTemplate(string firstName)
        {
            return "";
        }
    }
}
