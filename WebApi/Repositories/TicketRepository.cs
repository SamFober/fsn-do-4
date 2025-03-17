using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Interfaces.Repositories;
using WebApi.Models;
using Microsoft.Extensions.Logging;
using WebApi.Exceptions;

namespace WebApi.Repositories
{
    public class TicketRepository : ITicketRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TicketRepository> _logger;

        public TicketRepository(ApplicationDbContext context, ILogger<TicketRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        private List<Seat> FindBestAvailableSeats(List<Seat> availableSeats, Hall hall)
        {
            var middleRow = (int)Math.Ceiling(hall.Rows / 2.0);
            var middleSeat = (int)Math.Ceiling(hall.SeatsPerRow / 2.0);
            
            // Define the ideal viewing distance (about 2/3 back from the front)
            var idealRow = (int)Math.Ceiling(hall.Rows * 0.66);
            
            // Calculate scores for each seat
            var seatsWithScore = availableSeats.Select(seat =>
            {
                // Base distance score (Euclidean distance from the middle seat)
                var rowDistance = Math.Abs(seat.RowNumber - middleRow);
                var seatDistance = Math.Abs(seat.SeatNumber - middleSeat);
                var distanceScore = Math.Sqrt(rowDistance * rowDistance + seatDistance * seatDistance);

                // Viewing angle penalty (seats too close to the screen or too far to the sides)
                var viewingAnglePenalty = 0.0;
                if (seat.RowNumber <= 2) // First two rows
                {
                    viewingAnglePenalty += 5.0;
                    // Additional penalty for corner seats in front rows
                    if (seat.SeatNumber <= 2 || seat.SeatNumber >= hall.SeatsPerRow - 1)
                    {
                        viewingAnglePenalty += 3.0;
                    }
                }

                // Side seat penalty (increases towards the edges)
                var sideDistance = Math.Min(seat.SeatNumber, hall.SeatsPerRow - seat.SeatNumber + 1);
                var sidePenalty = Math.Max(0, 3 - sideDistance) * 2.0;

                // Ideal viewing distance bonus (prefer seats around 2/3 back)
                var idealRowDistance = Math.Abs(seat.RowNumber - idealRow);
                var idealRowBonus = idealRowDistance <= 2 ? -2.0 : 0.0; // Negative score is better

                // Combine all factors
                var totalScore = distanceScore + viewingAnglePenalty + sidePenalty + idealRowBonus;

                return new { Seat = seat, Score = totalScore };
            });

            // Order by score (lowest is best) and return all seats in that order
            return seatsWithScore
                .OrderBy(s => s.Score)
                .Select(s => s.Seat)
                .ToList();
        }

        public async Task<List<Seat>> GetAvailableSeats(int presentationId, bool findBestSeat = false)
        {
            // Get the presentation to get the hall ID and details
            var presentation = await _context.Presentations
                .Include(p => p.Hall)
                .FirstOrDefaultAsync(p => p.Id == presentationId);

            if (presentation == null)
            {
                _logger.LogWarning("Presentation {PresentationId} not found", presentationId);
                return new List<Seat>();
            }

            var bookedSeatIds = await _context.Tickets
                .Where(t => t.PresentationId == presentationId && 
                       t.Status != TicketStatus.Cancelled)
                .Select(t => t.SeatId)
                .ToListAsync();

            var lockedSeatIds = await _context.SeatLocks
                .Where(l => l.ExpiresAt > DateTime.UtcNow)
                .Select(l => l.SeatId)
                .ToListAsync();

            var unavailableSeatIds = bookedSeatIds.Concat(lockedSeatIds).ToList();

            var availableSeats = await _context.Seats
                .Where(s => s.HallId == presentation.HallId && 
                       !unavailableSeatIds.Contains(s.Id))
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            if (!findBestSeat || !availableSeats.Any())
                return availableSeats;

            return FindBestAvailableSeats(availableSeats, presentation.Hall);
        }

        public async Task<bool> AreSeatAvailable(int presentationId, List<int> seatIds, Guid? orderToken = null)
        {
            try
            {
                // First, verify the seats belong to the correct hall
                var presentation = await _context.Presentations
                    .Include(p => p.Hall)
                    .FirstOrDefaultAsync(p => p.Id == presentationId);

                if (presentation == null)
                {
                    _logger.LogWarning("Presentation {PresentationId} not found", presentationId);
                    return false;
                }

                // Get seats and verify they belong to the presentation's hall
                var seats = await _context.Seats
                    .Where(s => seatIds.Contains(s.Id))
                    .ToListAsync();

                var invalidSeats = seats.Where(s => s.HallId != presentation.HallId).ToList();
                if (invalidSeats.Any())
                {
                    _logger.LogWarning(
                        "Seats {SeatIds} do not belong to hall {HallId} for presentation {PresentationId}",
                        string.Join(", ", invalidSeats.Select(s => s.Id)),
                        presentation.HallId,
                        presentationId);
                    return false;
                }

                // Get booked seats (excluding cancelled tickets)
                var bookedSeats = await _context.Tickets
                    .Where(t => t.PresentationId == presentationId && 
                           t.Status != TicketStatus.Cancelled)
                    .Select(t => t.SeatId)
                    .ToListAsync();

                // Get locked seats - Only check locks from other orders
                var lockedSeats = await _context.SeatLocks
                    .Where(l => l.ExpiresAt > DateTime.UtcNow && 
                           seatIds.Contains(l.SeatId) &&
                           (!orderToken.HasValue || l.OrderToken != orderToken.Value))
                    .Select(l => l.SeatId)
                    .ToListAsync();

                // Get pending orders - Exclude current order
                var pendingSeats = await _context.TicketOrders
                    .Where(o => o.PresentationId == presentationId && 
                           o.Status == OrderStatus.Pending &&
                           o.ExpiresAt > DateTime.UtcNow &&
                           (!orderToken.HasValue || o.OrderToken != orderToken.Value))
                    .SelectMany(o => o.Items.Select(i => i.SeatId))
                    .ToListAsync();

                var unavailableSeats = new HashSet<int>();
                unavailableSeats.UnionWith(bookedSeats);
                unavailableSeats.UnionWith(lockedSeats);
                unavailableSeats.UnionWith(pendingSeats);

                // Check if any of the requested seats are unavailable
                var unavailableRequestedSeats = seatIds.Where(id => unavailableSeats.Contains(id)).ToList();
                
                if (unavailableRequestedSeats.Any())
                {
                    _logger.LogWarning(
                        "Seats {SeatIds} are not available for presentation {PresentationId}. " +
                        "Booked: {BookedSeats}, Locked: {LockedSeats}, Pending: {PendingSeats}",
                        string.Join(", ", unavailableRequestedSeats),
                        presentationId,
                        string.Join(", ", bookedSeats),
                        string.Join(", ", lockedSeats),
                        string.Join(", ", pendingSeats));
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error checking seat availability for presentation {PresentationId}", 
                    presentationId);
                throw;
            }
        }

        public async Task<List<Seat>> GetSeatsByIds(List<int> seatIds)
        {
            return await _context.Seats
                .Where(s => seatIds.Contains(s.Id))
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();
        }

        public async Task<List<SeatLock>> GetExpiredLocks()
        {
            return await _context.SeatLocks
                .Where(l => l.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();
        }

        public async Task RemoveSeatLocks(List<SeatLock> locks)
        {
            _context.SeatLocks.RemoveRange(locks);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> AddSeatLocks(List<SeatLock> locks)
        {
            try
            {
                await _context.SeatLocks.AddRangeAsync(locks);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<TicketOrder?> GetOrderByToken(Guid orderToken, bool includeItems = false)
        {
            var order = await _context.TicketOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderToken == orderToken);

            if (order != null && includeItems)
            {
                // Explicitly load the presentation and its related entities
                await _context.Entry(order)
                    .Reference(o => o.Presentation)
                    .LoadAsync();

                if (order.Presentation != null)
                {
                    await _context.Entry(order.Presentation)
                        .Reference(p => p.Movie)
                        .LoadAsync();

                    await _context.Entry(order.Presentation)
                        .Reference(p => p.Hall)
                        .LoadAsync();
                }
            }

            return order;
        }

        public async Task<bool> SaveOrder(TicketOrder order)
        {
            try
            {
                if (order.Id == 0)
                    _context.TicketOrders.Add(order);
                else
                    _context.TicketOrders.Update(order);

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<Ticket>> CreateTickets(List<Ticket> tickets)
        {
            await _context.Tickets.AddRangeAsync(tickets);
            await _context.SaveChangesAsync();
            return tickets;
        }

        public async Task<List<SeatLock>> GetLocksByOrderAndSeat(Guid orderToken, int seatId)
        {
            return await _context.SeatLocks
                .Where(l => l.OrderToken == orderToken && l.SeatId == seatId)
                .ToListAsync();
        }

        public async Task<List<SeatLock>> GetLocksByOrder(Guid orderToken)
        {
            return await _context.SeatLocks
                .Where(l => l.OrderToken == orderToken)
                .ToListAsync();
        }

        public async Task<Presentation?> GetPresentationById(int id)
        {
            return await _context.Presentations
                .Include(p => p.Movie)
                .Include(p => p.Hall)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task UpdateSeatAvailability(List<int> seatIds, bool isAvailable)
        {
            var seats = await _context.Seats.Where(s => seatIds.Contains(s.Id)).ToListAsync();
            foreach (var seat in seats)
            {
                seat.IsAvailable = isAvailable; // Update the seat availability
            }
            await _context.SaveChangesAsync(); // Save changes to the database
        }

        public async Task CancelOrder(Guid orderToken)
        {
            var order = await GetOrderByToken(orderToken);
            if (order == null)
            {
                throw new OrderNotFoundException($"Order with token {orderToken} not found");
            }

            // Get all seat IDs that need to be made available again
            var seatIds = new HashSet<int>();
            
            // Add seats from order items
            seatIds.UnionWith(order.Items.Select(i => i.SeatId));
            
            // Add seats from all available options
            seatIds.UnionWith(order.AvailableOptions.Values.SelectMany(o => o.SeatIds));

            // Get and remove all seat locks for this order
            var seatLocks = await GetLocksByOrder(orderToken);
            await RemoveSeatLocks(seatLocks);

            // Update order status to cancelled
            order.Status = OrderStatus.Cancelled;
            await SaveOrder(order);

            // Make seats available again
            await UpdateSeatAvailability(seatIds.ToList(), true);
        }

        public async Task<List<SeatLock>> GetSeatLocksByOrderToken(Guid orderToken)
        {
            return await _context.SeatLocks
                .Where(l => l.OrderToken == orderToken)
                .ToListAsync();
        }

        public async Task<TicketOrder?> FindTicketOrderByOrderToken(Guid orderToken)
        {
            return await _context.TicketOrders
                .Where(t => t.OrderToken == orderToken)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Ticket>> FindTicketsByOrderId(int orderId)
        {
            return await _context.Tickets
                .Where(t => t.TicketOrderId == orderId)
                .Include(t => t.Presentation)
                .ThenInclude(p => p.Hall)
                .Include(t => t.Presentation)
                .ThenInclude(p => p.Movie)
                .Include(t => t.Seat)
                .ToListAsync();
        }
    }
} 