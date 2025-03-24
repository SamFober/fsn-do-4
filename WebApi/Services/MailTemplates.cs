using WebApi.Models;

namespace WebApi.Services
{
    public class MailTemplates
    {
        public static string OrderCompleteMailTemplate(TicketOrder ticketOrder)
        {
            string templatePath = @"Resources/EmailTemplates/confirm_order_email_template.html";

            string emailTemplate = File.Exists(templatePath) ?
                File.ReadAllText(templatePath) : "Hi {firstName}, here are your tickets!";

            emailTemplate = emailTemplate.Replace("{firstName}", "");

            return emailTemplate;
        }
    }
}
