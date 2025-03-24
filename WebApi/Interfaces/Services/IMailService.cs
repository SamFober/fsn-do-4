namespace WebApi.Interfaces.Services
{
    public interface IMailService
    {
        Task<bool> SendEmail(string recipientName, string recipientAddress, string subject, string body, List<object>? attachments);
    }
}
