using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApi.Interfaces.Services;
using WebApi.Models;

namespace WebApi.Tests.Mocks.Services
{
    class TicketPdfServiceMock : ITicketPdfService
    {
        public byte[] CreatePdfTicketsAsByteArray(List<Ticket> tickets, Guid orderToken)
        {
            return new byte[1024];
        }
    }
}
