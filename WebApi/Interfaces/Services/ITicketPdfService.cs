using WebApi.Models;

namespace WebApi.Interfaces.Services
{
    public interface ITicketPdfService
    {
        public byte[] CreatePdfTicketsAsByteArray(List<Ticket> tickets, List<OrderConcessionItem>? concessionItems ,Guid orderToken);
    }
}
