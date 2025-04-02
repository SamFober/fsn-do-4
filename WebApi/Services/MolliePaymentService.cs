using Mollie.Api.Client;
using Mollie.Api.Client.Abstract;
using Mollie.Api.Models.Payment.Request;
using WebApi.Exceptions;
using WebApi.Interfaces.Repositories;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Responses.Payment;

namespace WebApi.Services
{
    public class MolliePaymentService : IPaymentService
    {
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
            var ticketOrder = await _ticketRepository.GetOrderByToken(orderToken, includeItems: true)
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
                RedirectUrl = $"https://21753e8f9d26e4c7cbb6f5f29e89c1b1.serveo.net/{orderToken}/finish",
                WebhookUrl = "https://400b0cc0a3560f8d0744d7651dc290bd.serveo.net/api/payment"
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

        public async Task<bool> ProcessMolliePaymentUpdate(string paymentId)
        {
            try
            {
                var molliePaymentResponse = await _paymentClient.GetPaymentAsync(paymentId);

                if (molliePaymentResponse == null)
                {
                    _logger.LogError("Mollie payment not found.");
                    return false;
                }

                var ticketOrder = await _ticketRepository.GetOrderByMolliePaymentid(paymentId)
                    ?? throw new OrderNotFoundException("No order found with the given Mollie payment ID.");

                if (molliePaymentResponse.Status == "paid")
                {
                    ticketOrder.Payment.PaymentStatus = PaymentStatus.Paid;
                    foreach (var ticket in ticketOrder.Tickets)
                    {
                        ticket.Status = TicketStatus.Paid;
                    }

                    return await _ticketRepository.SaveOrder(ticketOrder);
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
                    return await _ticketRepository.SaveOrder(ticketOrder);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating payment: ");
                return false;
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
    }
}
