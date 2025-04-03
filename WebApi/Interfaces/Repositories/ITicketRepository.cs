using WebApi.Models;

namespace WebApi.Interfaces.Repositories
{
    public interface ITicketRepository
    {
        Task<List<Seat>> GetAvailableSeats(int presentationId, bool findBest);
        Task<bool> AreSeatAvailable(int presentationId, List<int> seatIds, Guid? orderToken = null);
        Task<List<Seat>> GetSeatsByIds(List<int> seatIds);
        Task<Seat?> GetSeatById(int seatId);
        Task<List<SeatLock>> GetExpiredLocks();
        Task RemoveSeatLocks(List<SeatLock> locks);
        Task<bool> AddSeatLocks(List<SeatLock> locks);
        Task<TicketOrder?> GetOrderByToken(Guid orderToken, bool includeItems = false);
        Task<TicketOrder?> GetOnlineOrderByToken(Guid orderToken, bool includeItems = false);
        Task<TicketOrder?> GetOnlineOrderByMolliePaymentid(string molliePaymentId, bool includeItems = false);
        Task<bool> SaveOrder(TicketOrder order);
        Task<List<Ticket>> CreateTickets(List<Ticket> tickets);
        Task<List<SeatLock>> GetLocksByOrderAndSeat(Guid orderToken, int seatId);
        Task<Presentation?> GetPresentationById(int presentationId);
        Task UpdateSeatAvailability(List<int> seatIds, bool isAvailable, int presentationId);
        Task InitializeSeatPresentations(int presentationId);
        Task CancelOrder(Guid orderToken);
        Task<List<SeatLock>> GetSeatLocksByOrderToken(Guid orderToken);
        Task<TicketOrder?> FindTicketOrderByOrderToken(Guid orderToken);
        Task<List<Ticket>> FindTicketsByOrderId(int orderId);
        Task<List<Ticket>> FindTicketsByPhoneBookingCode(string phoneBookingCode);
        Task<List<SeatLock>> GetLocksByOrder(Guid orderToken);
        Task<List<SeatLock>> GetLocksByOrder(string orderToken);
        Task<ConcessionItem?> GetConcessionById(int concessionItemId);
        Task<bool> AddConcessionToOrder(OrderConcessionItem orderConcessionItem);
        Task<List<OrderConcessionItem>> FindConcessionItemsByOrderToken(Guid orderToken);
    }
}