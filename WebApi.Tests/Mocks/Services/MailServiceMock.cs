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
        public bool SendEmail(string recipientName, string recipientAddress, string subject, string body, List<object>? attachments)
        {
            return true;
        }
    }
}
