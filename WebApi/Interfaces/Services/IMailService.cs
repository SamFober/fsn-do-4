namespace WebApi.Interfaces.Services
{
    public interface IMailService
    {
        Task<bool> SendEmail(string recipient, string subject, string body, List<object> attachments);
    }
}
