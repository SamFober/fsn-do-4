using Microsoft.EntityFrameworkCore;
using WebApi.Exceptions;
using WebApi.Interfaces.Repositories;
using WebApi.Models;

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

            // Initialize SeatPresentations if they don't exist yet
            bool hasExistingRecords = await _context.SeatPresentations
                .AnyAsync(sp => sp.PresentationId == presentationId);
                
            if (!hasExistingRecords)
            {
                await InitializeSeatPresentations(presentationId);
            }

            // Get seat IDs that are unavailable from SeatPresentations
            var unavailableSeatIds = await _context.SeatPresentations
                .Where(sp => sp.PresentationId == presentationId && !sp.IsAvailable)
                .Select(sp => sp.SeatId)
                .ToListAsync();

            // Get locked seats
            var lockedSeatIds = await _context.SeatLocks
                .Where(l => l.PresentationId == presentationId && l.ExpiresAt > DateTime.UtcNow)
                .Select(l => l.SeatId)
                .ToListAsync();

            // Combine all unavailable seat IDs
            unavailableSeatIds = unavailableSeatIds.Concat(lockedSeatIds).Distinct().ToList();

            // Get available seats
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
                // Initialize SeatPresentations if they don't exist yet
                bool hasExistingRecords = await _context.SeatPresentations
                    .AnyAsync(sp => sp.PresentationId == presentationId);
                    
                if (!hasExistingRecords)
                {
                    _logger.LogWarning("No SeatPresentation records found for presentation {PresentationId}. Initializing...", presentationId);
                    await InitializeSeatPresentations(presentationId);
                    
                    // Double-check initialization was successful
                    hasExistingRecords = await _context.SeatPresentations
                        .AnyAsync(sp => sp.PresentationId == presentationId);
                        
                    if (!hasExistingRecords)
                    {
                        _logger.LogError("Failed to initialize SeatPresentation records for presentation {PresentationId}", presentationId);
                        return false;
                    }
                }

                // Get all requested seats to check their availability
                var requestedSeatPresentations = await _context.SeatPresentations
                    .Where(sp => sp.PresentationId == presentationId && 
                           seatIds.Contains(sp.SeatId))
                    .ToListAsync();
                    
                if (requestedSeatPresentations.Count < seatIds.Count)
                {
                    var missingSeatIds = seatIds.Except(requestedSeatPresentations.Select(sp => sp.SeatId)).ToList();
                    _logger.LogWarning(
                        "Some requested seats {MissingSeatIds} have no SeatPresentation records for presentation {PresentationId}",
                        string.Join(", ", missingSeatIds),
                        presentationId);
                    return false;
                }

                // If we have an order token, get the seats that are already locked by this order
                var currentOrderSeats = new List<int>();
                if (orderToken.HasValue)
                {
                    currentOrderSeats = await _context.SeatLocks
                        .Where(l => l.OrderToken == orderToken.Value && 
                                   l.PresentationId == presentationId && 
                                   l.ExpiresAt > DateTime.UtcNow)
                        .Select(l => l.SeatId)
                        .ToListAsync();
                    
                    _logger.LogInformation(
                        "Order {OrderToken} already has {LockCount} seats locked: {LockedSeats}",
                        orderToken.Value,
                        currentOrderSeats.Count,
                        string.Join(", ", currentOrderSeats));
                }

                // Get unavailable seats from SeatPresentations, excluding seats already locked by this order
                var unavailableSeats = requestedSeatPresentations
                    .Where(sp => !sp.IsAvailable && 
                                (orderToken == null || !currentOrderSeats.Contains(sp.SeatId)))
                    .Select(sp => sp.SeatId)
                    .ToList();

                if (unavailableSeats.Any())
                {
                    _logger.LogWarning(
                        "Seats {SeatIds} are not available for presentation {PresentationId} according to SeatPresentations",
                        string.Join(", ", unavailableSeats),
                        presentationId);
                    return false;
                }

                // Check if any of the seats are locked by other orders
                var lockedSeats = await _context.SeatLocks
                    .Where(l => l.ExpiresAt > DateTime.UtcNow &&
                           seatIds.Contains(l.SeatId) &&
                           l.PresentationId == presentationId &&
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

                // Check if any of the requested seats are locked or in pending orders
                var unavailableDueToLocks = new HashSet<int>();
                unavailableDueToLocks.UnionWith(lockedSeats);
                unavailableDueToLocks.UnionWith(pendingSeats);

                var unavailableRequestedSeats = seatIds.Where(id => unavailableDueToLocks.Contains(id)).ToList();

                if (unavailableRequestedSeats.Any())
                {
                    _logger.LogWarning(
                        "Seats {SeatIds} are not available for presentation {PresentationId}. " +
                        "Locked: {LockedSeats}, Pending: {PendingSeats}",
                        string.Join(", ", unavailableRequestedSeats),
                        presentationId,
                        string.Join(", ", lockedSeats),
                        string.Join(", ", pendingSeats));
                    return false;
                }

                _logger.LogInformation(
                    "All seats {SeatIds} are available for presentation {PresentationId}",
                    string.Join(", ", seatIds),
                    presentationId);
                    
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error checking seat availability for presentation {PresentationId} with seats {SeatIds}",
                    presentationId,
                    string.Join(", ", seatIds));
                // Return false instead of throwing to avoid crashing the application
                return false;
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
            try
            {
                return await _context.SeatLocks
                    .Where(l => l.OrderToken == orderToken)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locks by order token {OrderToken}", orderToken);
                return new List<SeatLock>();
            }
        }

        public async Task<List<SeatLock>> GetLocksByOrder(string orderToken)
        {
            try
            {
                // Parse the string orderToken to Guid
                if (Guid.TryParse(orderToken, out Guid orderGuid))
                {
                    return await GetLocksByOrder(orderGuid);
                }
                
                _logger.LogWarning("Failed to parse order token: {OrderToken}", orderToken);
                return new List<SeatLock>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving locks for order token: {OrderToken}", orderToken);
                return new List<SeatLock>();
            }
        }

        public async Task<Presentation?> GetPresentationById(int id)
        {
            return await _context.Presentations
                .Include(p => p.Movie)
                .Include(p => p.Hall)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task UpdateSeatAvailability(List<int> seatIds, bool isAvailable, int presentationId)
        {
            try
            {
                // Skip processing if no seat IDs are provided
                if (seatIds == null || !seatIds.Any())
                {
                    _logger.LogWarning("No seat IDs provided for updating availability for presentation {PresentationId}", presentationId);
                    return;
                }

                _logger.LogInformation(
                    "Updating seat availability for presentation {PresentationId}, Setting {SeatsCount} seats to {IsAvailable}: {SeatIds}",
                    presentationId, seatIds.Count, isAvailable ? "available" : "unavailable", string.Join(", ", seatIds));

                // Get the presentation
                var presentation = await _context.Presentations
                    .Include(p => p.Hall)
                    .FirstOrDefaultAsync(p => p.Id == presentationId);
                
                if (presentation == null || presentation.Hall == null)
                {
                    _logger.LogWarning("Presentation {PresentationId} or its Hall not found", presentationId);
                    return;
                }

                // Get all the seats to make sure they exist
                var seats = await _context.Seats
                    .Where(s => seatIds.Contains(s.Id) && s.HallId == presentation.HallId)
                    .ToListAsync();
                    
                if (seats.Count != seatIds.Count)
                {
                    var existingSeatIds = seats.Select(s => s.Id).ToList();
                    var missingSeatIds = seatIds.Except(existingSeatIds).ToList();
                    _logger.LogWarning(
                        "Some seats {MissingSeatIds} don't exist or don't belong to hall {HallId}",
                        string.Join(", ", missingSeatIds),
                        presentation.HallId);
                    
                    // Continue with the valid seats
                    seatIds = existingSeatIds;
                }

                // Track the actual number of seat availability changes
                int availabilityChangesCount = 0;

                // For each seat ID, update its availability
                foreach (var seatId in seatIds)
                {
                    try
                    {
                        // Find or create the SeatPresentation record
                        var seatPresentation = await _context.SeatPresentations
                            .FirstOrDefaultAsync(sp => sp.SeatId == seatId && sp.PresentationId == presentationId);
                        
                        if (seatPresentation != null)
                        {
                            // Only update if the availability status is actually changing
                            if (seatPresentation.IsAvailable != isAvailable)
                            {
                                // Update existing record
                                bool oldStatus = seatPresentation.IsAvailable;
                                seatPresentation.IsAvailable = isAvailable;
                                seatPresentation.UpdatedAt = DateTime.UtcNow;
                                
                                _logger.LogDebug("Updated SeatPresentation for seat {SeatId}, presentation {PresentationId} from {OldStatus} to {NewStatus}",
                                    seatId, presentationId, oldStatus, isAvailable);
                                
                                // Increment the change counter
                                availabilityChangesCount++;
                            }
                            else
                            {
                                _logger.LogDebug("Skipped updating SeatPresentation for seat {SeatId}, already {Status}",
                                    seatId, isAvailable ? "available" : "unavailable");
                            }
                        }
                        else
                        {
                            // Create new record if it doesn't exist
                            _context.SeatPresentations.Add(new SeatPresentation
                            {
                                SeatId = seatId,
                                PresentationId = presentationId,
                                IsAvailable = isAvailable,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            });
                            
                            _logger.LogDebug("Created new SeatPresentation for seat {SeatId}, presentation {PresentationId} with {IsAvailable}",
                                seatId, presentationId, isAvailable);
                            
                            // If we're creating a new record and marking it as unavailable, count it as a change
                            if (!isAvailable)
                            {
                                availabilityChangesCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating SeatPresentation for seat {SeatId}, presentation {PresentationId}",
                            seatId, presentationId);
                        // Continue with the rest of the seats
                    }
                }
                
                // Apply changes first before recounting
                await _context.SaveChangesAsync();
                
                // Calculate total seats in the hall
                int totalSeats = presentation.Hall.Rows * presentation.Hall.SeatsPerRow;
                
                // Count unavailable seats for this presentation
                int unavailableSeats = await _context.SeatPresentations
                    .CountAsync(sp => sp.PresentationId == presentationId && !sp.IsAvailable);
                
                // Update available seats count on the presentation
                int oldAvailableSeats = presentation.AvailableSeats;
                presentation.AvailableSeats = totalSeats - unavailableSeats;
                
                _logger.LogInformation("Updating presentation {PresentationId} available seats from {OldAvailableSeats} to {NewAvailableSeats} (total: {TotalSeats}, unavailable: {UnavailableSeats}, changes: {ChangesCount})", 
                    presentationId, oldAvailableSeats, presentation.AvailableSeats, totalSeats, unavailableSeats, availabilityChangesCount);
                
                // Save the updated presentation
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating seat availability for presentation {PresentationId} with seats {SeatIds}",
                    presentationId, string.Join(", ", seatIds));
            }
        }
        
        public async Task InitializeSeatPresentations(int presentationId)
        {
            // Get the presentation with its hall
            var presentation = await _context.Presentations
                .Include(p => p.Hall)
                .FirstOrDefaultAsync(p => p.Id == presentationId);
                
            if (presentation == null || presentation.Hall == null)
            {
                _logger.LogWarning("Cannot initialize seat presentations: Presentation {PresentationId} or its Hall not found", presentationId);
                return;
            }
            
            // Get all seats for this hall
            var seats = await _context.Seats
                .Where(s => s.HallId == presentation.HallId)
                .ToListAsync();
                
            // Check if any SeatPresentation records already exist for this presentation
            bool hasExistingRecords = await _context.SeatPresentations
                .AnyAsync(sp => sp.PresentationId == presentationId);
                
            if (hasExistingRecords)
            {
                _logger.LogInformation("SeatPresentation records already exist for presentation {PresentationId}", presentationId);
                return;
            }
            
            // Create SeatPresentation records for each seat
            var seatPresentations = seats.Select(seat => new SeatPresentation
            {
                SeatId = seat.Id,
                PresentationId = presentationId,
                IsAvailable = true,  // All seats are initially available
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            
            await _context.SeatPresentations.AddRangeAsync(seatPresentations);
            
            // Set the initial AvailableSeats count on the presentation
            presentation.AvailableSeats = seats.Count;
            
            _logger.LogInformation("Initialized {Count} seats for presentation {PresentationId}", 
                seats.Count, presentationId);
                
            await _context.SaveChangesAsync();
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
            await UpdateSeatAvailability(seatIds.ToList(), true, order.PresentationId);
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

        public async Task<List<Ticket>> FindTicketsByPhoneBookingCode(string phoneBookingCode)
        {
            return await _context.Tickets
                .Where(t => t.PhoneBookingCode == phoneBookingCode)
                .Include(t => t.Presentation)
                .ThenInclude(p => p.Hall)
                .Include(t => t.Presentation)
                .ThenInclude(p => p.Movie)
                .Include(t => t.Seat)
                .ToListAsync();
        }
        public async Task<List<OrderConcessionItem>?> FindConcessionItemsByOrderToken(Guid orderToken)
        {
            return await _context.OrderConcessionItems
                .Where(oci => oci.Order.OrderToken == orderToken)
                .Include(oci => oci.ConcessionItem)
                .ToListAsync();
        }

        public async Task<Seat?> GetSeatById(int seatId)
        {
            try
            {
                return await _context.Seats
                    .FirstOrDefaultAsync(s => s.Id == seatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting seat by ID {SeatId}", seatId);
                return null;
            }
        }

        public async Task<ConcessionItem?> GetConcessionById(int concessionItemId)
        {
            try
            {
                return await _context.ConcessionItems
                .FirstOrDefaultAsync(c => c.Id == concessionItemId);
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting concession item by ID {ConcessionItemId}", concessionItemId);
                return null;
            }
        }
        public async Task<bool> AddConcessionToOrder(OrderConcessionItem orderConcessionItem)
        {
            try
            {
                await _context.OrderConcessionItems.AddAsync(orderConcessionItem);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding concession item to order. Order ID: {OrderId}, ConcessionItem ID: {ConcessionItemId}",
                    orderConcessionItem.OrderId, orderConcessionItem.ConcessionItemId);
                return false;
            }
        }
    }
}