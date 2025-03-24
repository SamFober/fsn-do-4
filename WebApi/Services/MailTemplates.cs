using WebApi.Models;

namespace WebApi.Services
{
    public class MailTemplates
    {
        public static string OrderCompleteMailTemplate(TicketOrder ticketOrder)
        {

            string emailTemplate = LoadTemplate("confirm_order_email_template.html", "Hi {firstName}, here are your tickets!");

            emailTemplate = emailTemplate.Replace("{firstName}", "");

            return emailTemplate;
        }

        /// <summary>
        /// Loads an email HTML template file
        /// </summary>
        /// <param name="templateFileName">The template file name</param>
        /// <param name="fallBack">Fallback (HTML) string when failing to load the template.</param>
        /// <returns>The HTML template file as a string</returns>
        private static string LoadTemplate(string templateFileName, string fallBack)
        {
            string filePath = @"Resources/EmailTemplates/" + templateFileName;
            return File.Exists(filePath) ?
                File.ReadAllText(filePath) : fallBack;
        }
    }
}
