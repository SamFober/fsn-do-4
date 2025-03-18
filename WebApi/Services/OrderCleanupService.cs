using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Hosting;

namespace WebApi.Services
{
    public class OrderCleanupService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _services;
        private Timer? _timer;
        private readonly ILogger<OrderCleanupService> _logger;

        public OrderCleanupService(IServiceProvider services, ILogger<OrderCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                var expiredLocks = await context.SeatLocks
                    .Where(l => l.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync();

                if (expiredLocks.Any())
                {
                    context.SeatLocks.RemoveRange(expiredLocks);
                    _logger.LogInformation("Removed {Count} expired seat locks", expiredLocks.Count);
                }

                var expiredOrders = await context.TicketOrders
                    .Where(o => o.Status == OrderStatus.Pending && 
                               o.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync();

                foreach (var order in expiredOrders)
                {
                    order.Status = OrderStatus.Expired;
                    _logger.LogInformation("Marked order {OrderToken} as expired", order.OrderToken);
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up orders");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
} 