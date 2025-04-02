using Microsoft.AspNetCore.Mvc;
using WebApi.Exceptions;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Requests.Payment;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult> Webhook([FromForm] string id)
        {
            try
            {
                await _paymentService.ProcessMolliePaymentUpdate(id);
                return Ok();
            }
            catch (OrderNotFoundException ex)
            {
                return NotFound("Order not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500, "An error occurred while updating the payment");
            }
        }
    }
}
