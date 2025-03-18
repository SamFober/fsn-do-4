using System;

namespace WebApi.Models.Responses
{
    public class OrderStatusResponse
    {
        public Guid OrderToken { get; set; }
        public bool IsValid { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Status { get; set; }
    }
} 