using MailKit.Net.Smtp;
using MimeKit;
using WebApi.Interfaces.Services;
using WebApi.Models;

namespace WebApi.Services
{
    public class MailServiceMailKit : IMailService
    {
        private readonly SmtpClient client;
        private readonly ILogger<MailServiceMailKit> logger;

        private readonly string clientHost;
        private readonly int clientPort;
        private readonly string senderName;
        private readonly string senderAddress;
        private readonly string baseUrl; // Base URL for application links in emails

        public MailServiceMailKit(ILogger<MailServiceMailKit> logger, IConfiguration config)
        {
            client = new SmtpClient();
            this.logger = logger;

            clientHost = config["MailKitConfig:Host"] ?? "localhost";
            if (!Int32.TryParse(config["MailKitConfig:Port"], out clientPort))
            {
                clientPort = 1025;
            }
            senderAddress = config["MailKitConfig:SenderAddress"] ?? "default@example.com";
            senderName = config["MailKitConfig:SenderName"] ?? "Default Sender";
            
            // Get the base URL from configuration, or use a default
            // This should match the external URL users will access the app from
            baseUrl = config["AppConfig:BaseUrl"] ?? "http://localhost:5002"; 
        }

        // Helper method to replace placeholders in templates
        private string PrepareTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
                return template;
                
            return template.Replace("{BaseUrl}", baseUrl);
        }

        /// <summary>
        /// Sends an email to the given address
        /// </summary>
        /// <param name="recipientName">The name of the recipient</param>
        /// <param name="recipientAddress">The email address of the recipient</param>
        /// <param name="subject">The subject of the email</param>
        /// <param name="body">The (HTML) body of the email</param>
        /// <param name="attachments" cref="MimePart">Attachmants for the email. These need to be in the form of a MimeKit.MimePart object</param>
        /// <returns>A boolean indicating whether the email is succesfully sent or not.</returns>
        public bool SendEmail(string recipientName, string recipientAddress, string subject, string body, List<object>? attachments)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderAddress));
            message.To.Add(new MailboxAddress(recipientName, recipientAddress));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder();

            // Replace placeholders in the HTML body
            bodyBuilder.HtmlBody = PrepareTemplate(body);

            if (attachments != null && attachments.Count > 0)
            {
                foreach (var attachment in attachments)
                {
                    if (attachment is MimePart)
                    {
                        bodyBuilder.Attachments.Add(attachment as MimePart);
                    }
                    else
                    {
                        logger.LogWarning("Attachment skipped because it is not of type MimeKit.MimePart");
                    }
                }
            }

            message.Body = bodyBuilder.ToMessageBody();

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
