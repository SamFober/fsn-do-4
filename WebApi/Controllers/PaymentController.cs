using Microsoft.AspNetCore.Mvc;
using MimeKit;
using WebApi.Exceptions;
using WebApi.Interfaces.Repositories;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class PaymentController : ControllerBase
    {
        private readonly IMailService _mailService;
        private readonly ITicketPdfService _ticketPdfService;
        private readonly ITicketRepository _ticketRepository;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            ILogger<PaymentController> logger,
            ITicketPdfService ticketPdfService,
            ITicketRepository ticketRepository,
            IMailService mailService)
        {
            _mailService = mailService;
            _ticketRepository = ticketRepository;
            _ticketPdfService = ticketPdfService;
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult> Webhook([FromForm] string id)
        {
            try
            {
                var ticketOrder = await _paymentService.ProcessMolliePaymentUpdate(id);
                var tickets = await _ticketRepository.FindTicketsByOrderId(ticketOrder.Id);
                var concessionItems = await _ticketRepository.FindConcessionItemsByOrderToken(ticketOrder.OrderToken);

                var ticketBytes = _ticketPdfService.CreatePdfTicketsAsByteArray(tickets, concessionItems, ticketOrder.OrderToken);

                var ticketAttachments = new List<object>();
                {
                    new MimePart("application", "pdf")
                    {
                        Content = new MimeContent(new MemoryStream(ticketBytes)),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = $"{ticketOrder.OrderToken}.pdf"
                    };
                };

                _mailService.SendEmail(
                    $"{ticketOrder.Customer.FirstName} {ticketOrder.Customer.LastName}",
                    ticketOrder.Customer.EmailAddress,
                    $"Thank you for your order, {ticketOrder.Customer.FirstName}!",
                    MailTemplates.OrderCompleteMailTemplate(ticketOrder),
                    ticketAttachments);

                return Ok();
            }
            catch (OrderNotFoundException ex)
            {
                return NotFound("Order not found");
            }
            catch (PaymentNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                return StatusCode(500, "An error occurred while updating the payment");
            }
        }
    }
}
