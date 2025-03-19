using System;

namespace WebApi.Models;

public class SeatLock
{
    public int Id { get; set; }
    public int SeatId { get; set; }
    public int PresentationId { get; set; }
    public Guid OrderToken { get; set; }
    public Guid TicketOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Navigation property
    public Seat? Seat { get; set; }
}


