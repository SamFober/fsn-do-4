using WebApi.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebApi.Interfaces.Repositories
{
    public interface ITicketRepository
    {
        Task<List<Seat>> GetAvailableSeats(int presentationId, bool findBest);
        Task<bool> AreSeatAvailable(int presentationId, List<int> seatIds, Guid? orderToken = null);
        Task<List<Seat>> GetSeatsByIds(List<int> seatIds);
        Task<List<SeatLock>> GetExpiredLocks();
        Task RemoveSeatLocks(List<SeatLock> locks);
        Task<bool> AddSeatLocks(List<SeatLock> locks);
        Task<TicketOrder?> GetOrderByToken(Guid orderToken, bool includeItems = false);
        Task<bool> SaveOrder(TicketOrder order);
        Task<List<Ticket>> CreateTickets(List<Ticket> tickets);
        Task<List<SeatLock>> GetLocksByOrderAndSeat(Guid orderToken, int seatId);
        Task<Presentation?> GetPresentationById(int presentationId);
        Task UpdateSeatAvailability(List<int> seatIds, bool isAvailable);
        Task CancelOrder(Guid orderToken);
        Task<List<SeatLock>> GetSeatLocksByOrderToken(Guid orderToken);
    }
} 