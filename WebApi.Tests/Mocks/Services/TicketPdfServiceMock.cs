using WebApi.Interfaces.Services;
using WebApi.Models;

namespace WebApi.Tests.Mocks.Services
{
    class TicketPdfServiceMock : ITicketPdfService
    {
        public byte[] CreatePdfTicketsAsByteArray(List<Ticket> tickets, List<OrderConcessionItem>? concessionItems, Guid orderToken)
        {
            return new byte[1024];
        }
    }
}
