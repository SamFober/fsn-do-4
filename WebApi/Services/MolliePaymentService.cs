using Mollie.Api.Client;
using Mollie.Api.Client.Abstract;
using Mollie.Api.Models.Payment.Request;
using System.Text;
using WebApi.Exceptions;
using WebApi.Interfaces.Repositories;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Responses.Payment;

namespace WebApi.Services
{
    public class MolliePaymentService : IPaymentService
    {
        private readonly string _frontEndWebhookUrl;
        private readonly string _backEndWebhookUrl;

        private readonly ITicketRepository _ticketRepository;
        private readonly IMailService _mailService;
        private readonly IPaymentMethodClient _paymentMethodClient;
        private readonly IPaymentClient _paymentClient;
        private readonly ILogger<MolliePaymentService> _logger;

        public MolliePaymentService(
            IConfiguration config,
            ILogger<MolliePaymentService> logger,
            ITicketRepository ticketRepository,
            IMailService mailService)
        {
            var apiKey = config["Mollie:ApiKey"]
                ?? throw new MollieApiKeyNotSetException();

            _frontEndWebhookUrl = config["Serveo:FrontEndUrl"] 
                ?? throw new Exception("Serveo:FrontEndUrl not configured in appsettings.json or environment variables");
            
            _backEndWebhookUrl = config["Serveo:BackEndUrl"]
                ?? throw new Exception("Serveo:BackEndUrl not configured in appsettings.json or environment variables");

            _paymentMethodClient = new PaymentMethodClient(apiKey);
            _paymentClient = new PaymentClient(apiKey);
            _logger = logger;
            _ticketRepository = ticketRepository;
            _mailService = mailService;
        }

        public async Task<Payment> CreatePayment(Guid orderToken, Customer customer)
        {
            _logger.LogInformation($"Creating payment for order {orderToken}");
            decimal totalPaymentAmount = 0.00m;
            var ticketOrder = await _ticketRepository.GetOnlineOrderByToken(orderToken)
                ?? throw new OrderNotFoundException("Order not found.");

            if (ticketOrder.Payment != null) throw new PaymentAlreadyExistsException();

            var concessionItems = await _ticketRepository.FindConcessionItemsByOrderToken(orderToken);

            foreach (var item in ticketOrder.Items)
            {
                totalPaymentAmount += 12.50m;
            }

            foreach (var concessionItem in concessionItems)
            {
                totalPaymentAmount += (concessionItem.ConcessionItem.Price * concessionItem.Quantity);
            }

            var molliePaymentRequest = new PaymentRequest()
            {
                Description = $"Cinemagia order {orderToken}",
                Amount = new Mollie.Api.Models.Amount("EUR", totalPaymentAmount),
                RedirectUrl = $"{_frontEndWebhookUrl}/order/{orderToken}/complete",
                WebhookUrl = $"{_backEndWebhookUrl}/api/payment",
                CancelUrl = $"{_frontEndWebhookUrl}"
            };

            try
            {
                var molliePaymentResponse = await _paymentClient.CreatePaymentAsync(molliePaymentRequest);
                var payment = new Payment()
                {
                    MolliePaymentId = molliePaymentResponse.Id,
                    Amount = decimal.Parse(molliePaymentResponse.Amount.Value),
                    Description = molliePaymentResponse.Description,
                    CheckoutUrl = molliePaymentResponse.Links.Checkout.Href,
                    PaymentStatus = PaymentStatus.Pending
                };

                ticketOrder.Customer = customer;
                ticketOrder.Status = OrderStatus.Confirmed;
                ticketOrder.Payment = payment;

                await _ticketRepository.SaveOrder(ticketOrder);

                return payment;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<TicketOrder> ProcessMolliePaymentUpdate(string paymentId)
        {
            try
            {
                var molliePaymentResponse = await _paymentClient.GetPaymentAsync(paymentId);

                if (molliePaymentResponse == null)
                {
                    _logger.LogError("Mollie payment not found.");
                    throw new PaymentNotFoundException();
                }

                var ticketOrder = await _ticketRepository.GetOnlineOrderByMolliePaymentid(paymentId, includeItems: true)
                    ?? throw new OrderNotFoundException("No order found with the given Mollie payment ID.");

                if (molliePaymentResponse.Status == "paid")
                {
                    ticketOrder.Payment.PaymentStatus = PaymentStatus.Paid;
                    ticketOrder.OrderCode = GenerateOrderCode();

                    foreach (var ticket in ticketOrder.Tickets)
                    {
                        ticket.Status = TicketStatus.Paid;
                    }

                    await _ticketRepository.SaveOrder(ticketOrder);

                    return ticketOrder;
                }
                else
                {
                    switch (molliePaymentResponse.Status)
                    {
                        case "expired":
                            ticketOrder.Payment.PaymentStatus = PaymentStatus.Expired;
                            ticketOrder.Status = OrderStatus.Expired;
                            foreach (var ticket in ticketOrder.Tickets)
                            {
                                ticket.Status = TicketStatus.Cancelled;
                            }
                            break;

                        case "canceled":
                            ticketOrder.Payment.PaymentStatus = PaymentStatus.Canceled;
                            ticketOrder.Status = OrderStatus.Cancelled;
                            foreach (var ticket in ticketOrder.Tickets)
                            {
                                ticket.Status = TicketStatus.Cancelled;
                            }
                            break;
                    }

                    await _ticketRepository.SaveOrder(ticketOrder);

                    return ticketOrder;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating payment: ");
                throw;
            }
        }

        public async Task<List<PaymentMethodResponse>> GetAvailablePaymentMethods()
        {
            var paymentMethodListResponse = new List<PaymentMethodResponse>();

            var molliePaymentMethodList = await _paymentMethodClient.GetPaymentMethodListAsync(locale: "nl_NL", includeIssuers: true);

            foreach (var molliePaymentMethod in molliePaymentMethodList.Items)
            {
                var paymentIssuerResponses = new List<PaymentIssuerResponse>();

                if (molliePaymentMethod.Issuers != null)
                {
                    //Skip iDEAL issuers because of iDEAL 2.0
                    if (!molliePaymentMethod.Description.Equals("iDEAL"))
                    {
                        foreach (var molliePaymentIssuer in molliePaymentMethod.Issuers)
                        {
                            var paymentIssuer = new PaymentIssuerResponse() { Id = molliePaymentIssuer.Id, IssuerImage = molliePaymentIssuer.Image.Svg, Name = molliePaymentIssuer.Name };
                            paymentIssuerResponses.Add(paymentIssuer);
                        }
                    }
                }

                var paymentMethodResponse = new PaymentMethodResponse() { Id = molliePaymentMethod.Id, Description = molliePaymentMethod.Description, Image = molliePaymentMethod.Image.Svg, PaymentIssuers = paymentIssuerResponses };
                paymentMethodListResponse.Add(paymentMethodResponse);
            }

            return paymentMethodListResponse;
        }

        public Task GetPayments()
        {
            throw new NotImplementedException();
        }

        private string GenerateOrderCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder result = new StringBuilder(7);
            Random random = new Random();

            for (int i = 0; i < 7; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }

            return result.ToString();
        }
    }
}
