using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                // Get current date and first day of current month
                var today = DateTime.Today;
                var currentMonth = new DateTime(today.Year, today.Month, 1);
                var lastMonth = currentMonth.AddMonths(-1);

                // Count active movies
                var activeMoviesCount = await _context.Movies
                    .Where(m => m.IsActive)
                    .CountAsync();

                // Count active movies last month (to calculate growth)
                var lastMonthMoviesCount = await _context.Movies
                    .Where(m => m.CreatedAt < lastMonth && (m.IsActive || m.UpdatedAt >= lastMonth))
                    .CountAsync();

                // Calculate movies growth percentage
                double moviesGrowth = lastMonthMoviesCount > 0 
                    ? Math.Round(((double)activeMoviesCount - lastMonthMoviesCount) / lastMonthMoviesCount * 100, 1)
                    : 0;

                // Count tickets sold this month
                var ticketsThisMonth = await _context.Tickets
                    .Where(t => t.CreatedAt >= currentMonth && t.Status != TicketStatus.Cancelled)
                    .CountAsync();

                // Count tickets sold last month
                var ticketsLastMonth = await _context.Tickets
                    .Where(t => t.CreatedAt >= lastMonth && t.CreatedAt < currentMonth && t.Status != TicketStatus.Cancelled)
                    .CountAsync();

                // Calculate tickets growth percentage
                double ticketsGrowth = ticketsLastMonth > 0 
                    ? Math.Round(((double)ticketsThisMonth - ticketsLastMonth) / ticketsLastMonth * 100, 1)
                    : 0;

                // Calculate total revenue this month (sum of ticket prices)
                var ticketsWithPrice = await _context.Tickets
                    .Where(t => t.CreatedAt >= currentMonth && t.Status != TicketStatus.Cancelled)
                    .Include(t => t.Presentation)
                    .ToListAsync();

                decimal revenueThisMonth = ticketsWithPrice.Sum(t => t.Presentation?.Price ?? 0);

                // Calculate total revenue last month
                var lastMonthTicketsWithPrice = await _context.Tickets
                    .Where(t => t.CreatedAt >= lastMonth && t.CreatedAt < currentMonth && t.Status != TicketStatus.Cancelled)
                    .Include(t => t.Presentation)
                    .ToListAsync();

                decimal revenueLastMonth = lastMonthTicketsWithPrice.Sum(t => t.Presentation?.Price ?? 0);

                // Calculate revenue growth percentage
                double revenueGrowth = revenueLastMonth > 0 
                    ? Math.Round(((double)(revenueThisMonth - revenueLastMonth) / (double)revenueLastMonth) * 100, 1)
                    : 0;

                // Count active presentations
                var activePresentations = await _context.Presentations
                    .Where(p => p.EndTime > DateTime.UtcNow)
                    .CountAsync();

                // Count active presentations last month
                var lastMonthActivePresentations = await _context.Presentations
                    .Where(p => p.EndTime > lastMonth && p.EndTime <= currentMonth)
                    .CountAsync();

                // Calculate presentations growth percentage
                double presentationsGrowth = lastMonthActivePresentations > 0 
                    ? Math.Round(((double)activePresentations - lastMonthActivePresentations) / lastMonthActivePresentations * 100, 1)
                    : 0;

                // Return statistics
                return Ok(new {
                    MoviesCount = activeMoviesCount,
                    MoviesGrowthPercent = moviesGrowth,
                    TicketsCount = ticketsThisMonth,
                    TicketsGrowthPercent = ticketsGrowth,
                    RevenueTotal = revenueThisMonth,
                    RevenueGrowthPercent = revenueGrowth,
                    PresentationsCount = activePresentations,
                    PresentationsGrowthPercent = presentationsGrowth
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard stats");
                return StatusCode(500, "An error occurred while fetching dashboard statistics");
            }
        }

        [HttpGet("chart-data")]
        public async Task<IActionResult> GetChartData([FromQuery] int? timeRange = 7, [FromQuery] string? metric = "tickets")
        {
            try
            {
                // Determine date range based on timeRange parameter
                var today = DateTime.Today;
                DateTime startDate;
                
                // Handle different time ranges
                switch (timeRange)
                {
                    case 30:
                        startDate = today.AddDays(-29);
                        break;
                    case 90:
                        startDate = today.AddDays(-89);
                        break;
                    case 365:
                        startDate = today.AddDays(-364);
                        break;
                    default: // Default to 7 days
                        startDate = today.AddDays(-6);
                        break;
                }
                
                // Format labels based on the time range
                string[] labels;
                if (timeRange >= 365)
                {
                    // For a year, show months
                    var months = new string[12];
                    var startMonth = startDate.Month;
                    for (int i = 0; i < 12; i++)
                    {
                        var monthIndex = (startMonth + i - 1) % 12 + 1;
                        months[i] = new DateTime(today.Year, monthIndex, 1).ToString("MMM");
                    }
                    labels = months;
                }
                else if (timeRange >= 90)
                {
                    // For 90 days, group by weeks (13 weeks)
                    var weeks = new string[13];
                    for (int i = 0; i < 13; i++)
                    {
                        weeks[i] = $"Week {i + 1}";
                    }
                    labels = weeks;
                }
                else if (timeRange >= 30)
                {
                    // For 30 days, show day numbers
                    var days = new string[30];
                    for (int i = 0; i < 30; i++)
                    {
                        days[i] = startDate.AddDays(i).Day.ToString();
                    }
                    labels = days;
                }
                else
                {
                    // For 7 days, show day names
                    labels = Enumerable.Range(0, 7)
                        .Select(i => startDate.AddDays(i).ToString("ddd"))
                        .ToArray();
                }
                
                // Get tickets for the selected date range
                var ticketQuery = _context.Tickets
                    .Where(t => t.CreatedAt.Date >= startDate && t.CreatedAt.Date <= today && t.Status != TicketStatus.Cancelled)
                    .Include(t => t.Presentation)
                    .ThenInclude(p => p.Movie);
                
                var tickets = await ticketQuery.ToListAsync();
                
                // Prepare revenue chart data
                object revenueData;
                
                if (timeRange >= 365)
                {
                    // Group by month for yearly view
                    var revenueByMonth = Enumerable.Range(0, 12)
                        .Select(i => {
                            var monthStart = new DateTime(today.Year, ((startDate.Month + i - 1) % 12) + 1, 1);
                            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                            var monthTickets = tickets.Where(t => 
                                t.CreatedAt.Month == monthStart.Month);
                            return monthTickets.Sum(t => t.Presentation?.Price ?? 0);
                        })
                        .ToArray();
                    
                    revenueData = new {
                        labels = labels,
                        datasets = new[]
                        {
                            new
                            {
                                label = "Revenue (€)",
                                data = revenueByMonth,
                                backgroundColor = "rgba(245, 197, 24, 0.2)",
                                borderColor = "rgba(245, 197, 24, 1)",
                                borderWidth = 2,
                                tension = 0.3
                            }
                        }
                    };
                }
                else if (timeRange >= 90)
                {
                    // Group by week for 90-day view
                    var revenueByWeek = Enumerable.Range(0, 13)
                        .Select(i => {
                            var weekStart = startDate.AddDays(i * 7);
                            var weekEnd = weekStart.AddDays(6);
                            var weekTickets = tickets.Where(t => 
                                t.CreatedAt.Date >= weekStart && t.CreatedAt.Date <= weekEnd);
                            return weekTickets.Sum(t => t.Presentation?.Price ?? 0);
                        })
                        .ToArray();
                    
                    revenueData = new {
                        labels = labels,
                        datasets = new[]
                        {
                            new
                            {
                                label = "Revenue (€)",
                                data = revenueByWeek,
                                backgroundColor = "rgba(245, 197, 24, 0.2)",
                                borderColor = "rgba(245, 197, 24, 1)",
                                borderWidth = 2,
                                tension = 0.3
                            }
                        }
                    };
                }
                else if (timeRange >= 30)
                {
                    // Daily data for 30-day view
                    var revenueByDay = Enumerable.Range(0, 30)
                        .Select(i => {
                            var date = startDate.AddDays(i);
                            var dateTickets = tickets.Where(t => t.CreatedAt.Date == date);
                            return dateTickets.Sum(t => t.Presentation?.Price ?? 0);
                        })
                        .ToArray();
                    
                    revenueData = new {
                        labels = labels,
                        datasets = new[]
                        {
                            new
                            {
                                label = "Revenue (€)",
                                data = revenueByDay,
                                backgroundColor = "rgba(245, 197, 24, 0.2)",
                                borderColor = "rgba(245, 197, 24, 1)",
                                borderWidth = 2,
                                tension = 0.3
                            }
                        }
                    };
                }
                else
                {
                    // Daily data for 7-day view
                    var revenueByDay = Enumerable.Range(0, 7)
                        .Select(i => {
                            var date = startDate.AddDays(i);
                            var dateTickets = tickets.Where(t => t.CreatedAt.Date == date);
                            return dateTickets.Sum(t => t.Presentation?.Price ?? 0);
                        })
                        .ToArray();
                    
                    revenueData = new {
                        labels = labels,
                        datasets = new[]
                        {
                            new
                            {
                                label = "Revenue (€)",
                                data = revenueByDay,
                                backgroundColor = "rgba(245, 197, 24, 0.2)",
                                borderColor = "rgba(245, 197, 24, 1)",
                                borderWidth = 2,
                                tension = 0.3
                            }
                        }
                    };
                }
                
                // Get top 5 movies based on the selected metric (tickets or revenue)
                var movieGroups = tickets
                    .Where(t => t.Presentation?.Movie != null)
                    .GroupBy(t => t.Presentation.Movie.Title)
                    .Select(g => new {
                        MovieTitle = g.Key,
                        TicketCount = g.Count(),
                        Revenue = g.Sum(t => t.Presentation.Price)
                    });
                
                // Sort by the selected metric
                if (metric?.ToLower() == "revenue")
                {
                    movieGroups = movieGroups.OrderByDescending(m => m.Revenue);
                }
                else
                {
                    movieGroups = movieGroups.OrderByDescending(m => m.TicketCount);
                }
                
                // Take top 5
                var topMovies = movieGroups.Take(5).ToList();
                
                // Prepare movie chart data
                object movieData;
                
                if (metric?.ToLower() == "revenue")
                {
                    // Revenue data for movies
                    movieData = new {
                        labels = topMovies.Select(m => m.MovieTitle).ToArray(),
                        datasets = new[]
                        {
                            new
                            {
                                label = "Revenue (€)",
                                data = topMovies.Select(m => (double)m.Revenue).ToArray(),
                                backgroundColor = new[] { 
                                    "rgba(54, 162, 235, 0.6)",
                                    "rgba(75, 192, 192, 0.6)",
                                    "rgba(255, 206, 86, 0.6)",
                                    "rgba(255, 99, 132, 0.6)",
                                    "rgba(153, 102, 255, 0.6)" 
                                },
                                borderWidth = 1
                            }
                        }
                    };
                }
                else
                {
                    // Ticket count data for movies
                    movieData = new {
                        labels = topMovies.Select(m => m.MovieTitle).ToArray(),
                        datasets = new[]
                        {
                            new
                            {
                                label = "Tickets Sold",
                                data = topMovies.Select(m => m.TicketCount).ToArray(),
                                backgroundColor = new[] { 
                                    "rgba(54, 162, 235, 0.6)",
                                    "rgba(75, 192, 192, 0.6)",
                                    "rgba(255, 206, 86, 0.6)",
                                    "rgba(255, 99, 132, 0.6)",
                                    "rgba(153, 102, 255, 0.6)" 
                                },
                                borderWidth = 1
                            }
                        }
                    };
                }
                
                return Ok(new {
                    RevenueData = revenueData,
                    MovieData = movieData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chart data");
                return StatusCode(500, "An error occurred while fetching chart data");
            }
        }

        [HttpGet("nav-counts")]
        public async Task<IActionResult> GetNavCounts()
        {
            try
            {
                // Get counts for navbar badges
                var activeMoviesCount = await _context.Movies
                    .Where(m => m.IsActive)
                    .CountAsync();
                
                var activePresentations = await _context.Presentations
                    .Where(p => p.EndTime > DateTime.UtcNow)
                    .CountAsync();
                
                var hallsCount = await _context.Halls
                    .CountAsync();
                
                var ticketsCount = await _context.Tickets
                    .Where(t => t.Status != TicketStatus.Cancelled)
                    .CountAsync();
                
                // Since Orders table doesn't exist, we'll just use a default count
                // or you can replace this with another relevant count from an existing table
                var newOrdersCount = 5; // Default count until proper implementation
                
                return Ok(new {
                    Movies = activeMoviesCount,
                    Presentations = activePresentations,
                    Halls = hallsCount,
                    Tickets = ticketsCount,
                    NewOrders = newOrdersCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching navigation counts");
                return StatusCode(500, "An error occurred while fetching navigation counts");
            }
        }
    }
} 