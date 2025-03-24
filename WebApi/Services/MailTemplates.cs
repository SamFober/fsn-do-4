using WebApi.Models;

namespace WebApi.Services
{
    public class MailTemplates
    {
        public static string OrderCompleteMailTemplate(TicketOrder ticketOrder)
        {
            string templatePath = @"Resources/EmailTemplates/confirm_order_email_template.html";

            string emailTemplate = LoadTemplate(templatePath, "Hi {firstName}, here are your tickets!");

            emailTemplate = emailTemplate.Replace("{firstName}", "");

            return emailTemplate;
        }

        /// <summary>
        /// Loads an email HTML template file
        /// </summary>
        /// <param name="templatePath">The path to the HTML file</param>
        /// <param name="fallBack">Fallback (HTML) string when failing to load the template.</param>
        /// <returns>The HTML template file as a string</returns>
        private static string LoadTemplate(string templatePath, string fallBack)
        {
            return File.Exists(templatePath) ?
                File.ReadAllText(templatePath) : fallBack;
        }
    }
}
