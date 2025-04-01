using Mollie.Api.Client;
using Mollie.Api.Client.Abstract;
using Mollie.Api.Models.Payment.Request;
using WebApi.Exceptions;
using WebApi.Interfaces.Repositories;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Exceptions;
using WebApi.Models.Responses.Payment;

namespace WebApi.Services
{
    public class MolliePaymentService : IPaymentService
    {
        private readonly ITicketRepository _ticketRepository;
        private readonly IPaymentMethodClient _paymentMethodClient;
        private readonly IPaymentClient _paymentClient;
        private readonly ILogger<MolliePaymentService> _logger;

        public MolliePaymentService(IConfiguration config, ILogger<MolliePaymentService> logger, ITicketRepository ticketRepository)
        {
            var apiKey = config["Mollie:ApiKey"]
                ?? throw new MollieApiKeyNotSetException();

            _paymentMethodClient = new PaymentMethodClient(apiKey);
            _paymentClient = new PaymentClient(apiKey);
            _logger = logger;
            _ticketRepository = ticketRepository;
        }

        public async Task<Payment> CreatePayment(Guid orderToken)
        {
            decimal totalPaymentAmount = 0.00m;
            var ticketOrder = await _ticketRepository.GetOrderByToken(orderToken, includeItems: true) 
                ?? throw new OrderNotFoundException("Order not found.");

            if (ticketOrder.Payment != null) throw new Exception("Payment for this order already exists.");
            
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
                RedirectUrl = "https://www.google.nl",
                WebhookUrl = "https://www.google.nl",
                Method = ""
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
                    PaymentStatus = PaymentStatus.Pending,
                };

                ticketOrder.Payment = payment;

                await _ticketRepository.SaveOrder(ticketOrder);


                return payment;
            }
            catch(Exception ex)
            {
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

        public Task UpdatePayment()
        {
            throw new NotImplementedException();
        }
    }
}
