namespace WebApi.Interfaces.Services
{
    public interface IMailService
    {
        Task<bool> SendEmai(string recipient, string subject, string body, List<object>? attachments);
    }
}
