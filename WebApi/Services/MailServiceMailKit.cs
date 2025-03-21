using MailKit.Net.Smtp;
using MimeKit;
using WebApi.Interfaces.Services;

namespace WebApi.Services
{
    public class MailServiceMailKit : IMailService
    {
        private readonly SmtpClient client;
        private readonly ILogger<MailServiceMailKit> logger;
        private readonly string clientHost;
        private readonly int clientPort;

        public MailServiceMailKit(ILogger<MailServiceMailKit> logger, IConfiguration config)
        {
            this.client = new SmtpClient();
            this.logger = logger;
            this.clientHost = config["MailKitConfig:Host"] ?? "localhost";
            if (!Int32.TryParse(config["MailKitConfig:Port"], out clientPort))
            {
                clientPort = 1025;
            }
        }

        public async Task<bool> SendEmai(string recipient, string subject, string body, List<object>? attachments)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Cinemagia", "cinemagia@example.com"));
            message.To.Add(new MailboxAddress("recipient", recipient));
            message.Subject = subject;
            var messageBody = new TextPart("plain")
            {
                Text = body
            };

            var multipart = new Multipart("mixed");

            if (attachments != null && attachments.Count > 0)
            {
                foreach (var attachment in attachments)
                {
                    if (attachment is MimePart)
                    {
                        multipart.Add(attachment as MimePart);
                    }
                    else
                    {
                        logger.LogWarning("Attachment skipped because it is not of type MimeKit.MimePart");
                    }
                }
            }

            multipart.Add(messageBody);
            message.Body = multipart;

            try
            {
                client.Connect(clientHost, clientPort, false);
                client.Send(message);
                client.Disconnect(true);
                return true;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to send email");
                return false;
            }
        }
    }
}
