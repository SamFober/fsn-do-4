namespace WebApi.Models.Requests
{
    public class CancelTicketRequest
    {
        public string Reason { get; set; } = "";
        public bool IssueRefund { get; set; }
    }
} 