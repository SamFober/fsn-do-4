using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApi.Models;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PresentationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PresentationsController> _logger;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10); // Default cache duration

        public PresentationsController(ApplicationDbContext context, IMemoryCache cache, ILogger<PresentationsController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public record CreatePresentationRequest(
            int MovieId,
            int HallId,
            DateTime StartTime,
            DateTime EndTime,
            decimal Price
        );

        public record PresentationResponse(
            int Id,
            string MovieTitle,
            string HallName,
            DateTime StartTime,
            DateTime EndTime,
            decimal Price,
            int AvailableSeats
        );

        [HttpGet("{id}/seats")]
        public async Task<ActionResult<object>> GetPresentationSeats(int id)
        {
            var presentation = await _context.Presentations
                .Include(p => p.Movie)
                .Include(p => p.Hall)
                .Include(p => p.Tickets)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (presentation == null)
            {
                return NotFound("Presentation not found.");
            }

            // Retrieve occupied seat IDs
            var occupiedSeatIds = presentation.Tickets
                .Where(t => t.Status != TicketStatus.Cancelled)
                .Select(t => t.SeatId)
                .ToHashSet();

            // Retrieve the seats from the database with the latest availability status
            var seats = await _context.Seats
                .AsNoTracking() // Ensure fresh data is retrieved
                .Where(s => s.HallId == presentation.Hall.Id)
                .ToListAsync();

            // Calculate seat availability
            var seatAvailability = seats
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .Select(s => new
                {
                    s.Id,
                    s.RowNumber,
                    s.SeatNumber,
                    IsAvailable = !occupiedSeatIds.Contains(s.Id) // Only check if not in occupied seats list
                })
                .GroupBy(s => s.RowNumber)
                .Select(g => new
                {
                    RowNumber = g.Key,
                    Seats = g.ToList()
                });

            var result = new
            {
                presentation.Id,
                presentation.Movie.Title,
                presentation.StartTime,
                presentation.EndTime,
                presentation.Price,
                presentation.IsSecretMovie,
                HallName = presentation.Hall.Name,
                Rows = seatAvailability
            };

            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetPresentations()
        {
            var presentations = await _context.Presentations
                .Include(p => p.Movie)
                .Include(p => p.Hall)
                .Select(p => new
                {
                    p.Id,
                    p.Movie.Title,
                    p.StartTime,
                    p.EndTime,
                    p.Price,
                    HallName = p.Hall.Name,
                    AvailableSeats = (p.Hall.Rows * p.Hall.SeatsPerRow) -
                        p.Tickets.Count(t => t.Status != TicketStatus.Cancelled)
                })
                .ToListAsync();

            return Ok(presentations);
        }

        [HttpPost]
        public async Task<ActionResult<PresentationResponse>> CreatePresentation(CreatePresentationRequest request)
        {
            var hall = await _context.Halls.FindAsync(request.HallId);
            if (hall == null)
            {
                return NotFound("Hall not found");
            }

            var movie = await _context.Movies.FindAsync(request.MovieId);
            if (movie == null)
            {
                return NotFound("Movie not found");
            }

            // Calculate capacity from rows and seats
            var capacity = hall.Rows * hall.SeatsPerRow;
            if (capacity == 0)
            {
                return BadRequest("Hall has no seats configured");
            }

            var presentation = new Presentation
            {
                MovieId = request.MovieId,
                HallId = request.HallId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Price = request.Price,
                Movie = movie,
                Hall = hall
            };

            _context.Presentations.Add(presentation);
            await _context.SaveChangesAsync();

            return Ok(new PresentationResponse(
                presentation.Id,
                movie.Title,
                hall.Name,
                presentation.StartTime,
                presentation.EndTime,
                presentation.Price,
                capacity // All seats available initially
            ));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetPresentation(int id)
        {
            var presentation = await _context.Presentations
                .Include(p => p.Hall)
                .Include(p => p.Movie)
                .Include(p => p.Tickets)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (presentation == null)
            {
                Console.WriteLine($"Presentation with ID {id} not found.");
                return NotFound();
            }

            // Return a simplified object
            var response = new
            {
                presentation.Id,
                MovieTitle = presentation.Movie.Title,
                HallName = presentation.Hall.Name,
                presentation.StartTime,
                presentation.EndTime,
                presentation.Price
            };

            Console.WriteLine($"Fetched Presentation: {id}, Hall: {presentation.Hall.Name}, Movie: {presentation.Movie.Title}");
            return Ok(response);
        }

        /// <summary>
        /// Get movie schedule (presentations) by date range with filtering and sorting options
        /// </summary>
        /// <param name="startDate">Optional start date for the schedule (default: today)</param>
        /// <param name="endDate">Optional end date for the schedule (default: 7 days from start date)</param>
        /// <param name="movieId">Optional filter by movie ID</param>
        /// <param name="hallId">Optional filter by hall ID</param>
        /// <param name="format">Optional filter by format (e.g., "2D", "IMAX")</param>
        /// <param name="sortBy">Sort by: "time" (default), "popularity" (based on available seats)</param>
        /// <returns>List of presentations grouped by date and movie</returns>
        [HttpGet("schedule")]
        public async Task<ActionResult<object>> GetSchedule(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? movieId = null,
            [FromQuery] int? hallId = null,
            [FromQuery] string? format = null,
            [FromQuery] string sortBy = "time")
        {
            try
            {
                // Set default date range if not provided
                var actualStartDate = startDate ?? DateTime.Today;
                var actualEndDate = endDate ?? actualStartDate.AddDays(7);

                // Validate date range
                if (actualEndDate < actualStartDate)
                {
                    return BadRequest("End date cannot be earlier than start date");
                }

                // Create cache key based on parameters
                string cacheKey = $"schedule_{actualStartDate:yyyyMMdd}_{actualEndDate:yyyyMMdd}" +
                                  $"_{movieId}_{hallId}_{format}_{sortBy}";

                // Try to get from cache first
                if (_cache.TryGetValue(cacheKey, out object cachedSchedule))
                {
                    _logger.LogInformation("Retrieved schedule from cache with key: {CacheKey}", cacheKey);
                    return Ok(cachedSchedule);
                }

                // Build basic query for all presentations within date range
                var query = _context.Presentations
                    .Include(p => p.Movie)
                    .Include(p => p.Hall)
                    .Include(p => p.Tickets.Where(t => t.Status != TicketStatus.Cancelled))
                    .Where(p => p.StartTime.Date >= actualStartDate.Date && 
                                p.StartTime.Date <= actualEndDate.Date);

                // Apply optional filters
                if (movieId.HasValue)
                {
                    query = query.Where(p => p.MovieId == movieId.Value);
                }

                if (hallId.HasValue)
                {
                    query = query.Where(p => p.HallId == hallId.Value);
                }

                if (!string.IsNullOrEmpty(format))
                {
                    query = query.Where(p => p.Format.ToLower() == format.ToLower());
                }

                // Execute query and get presentations
                var presentations = await query.ToListAsync();

                // Calculate available seats for each presentation
                foreach (var presentation in presentations)
                {
                    presentation.AvailableSeats = (presentation.Hall.Rows * presentation.Hall.SeatsPerRow) -
                        presentation.Tickets.Count(t => t.Status != TicketStatus.Cancelled);
                }

                // Apply sorting
                IEnumerable<Presentation> sortedPresentations;
                switch (sortBy.ToLower())
                {
                    case "popularity":
                        // Sort by number of occupied seats (most popular first)
                        sortedPresentations = presentations
                            .OrderByDescending(p => p.Tickets.Count(t => t.Status != TicketStatus.Cancelled));
                        break;
                    case "time":
                    default:
                        // Sort by start time (default)
                        sortedPresentations = presentations.OrderBy(p => p.StartTime);
                        break;
                }

                // Group presentations by date and movie
                var groupedByDate = sortedPresentations
                    .GroupBy(p => p.StartTime.Date)
                    .OrderBy(g => g.Key)
                    .Select(dateGroup => new
                    {
                        Date = dateGroup.Key.ToString("yyyy-MM-dd"),
                        DayOfWeek = dateGroup.Key.DayOfWeek.ToString(),
                        Movies = dateGroup
                            .GroupBy(p => p.MovieId)
                            .Select(movieGroup => new
                            {
                                MovieId = movieGroup.Key,
                                Title = movieGroup.First().Movie.Title,
                                Duration = movieGroup.First().Movie.DurationMinutes,
                                Formats = movieGroup.Select(p => p.Format).Distinct().ToList(),
                                PosterUrl = movieGroup.First().Movie.PosterUrl,
                                Showtimes = movieGroup.Select(p => 
                                {
                                    // Calculate total seats and available seats
                                    int totalSeats = p.Hall.Rows * p.Hall.SeatsPerRow;
                                    int availableSeats = p.AvailableSeats;
                                    
                                    // Calculate if it's almost full - less than 15% seats available
                                    bool isAlmostFull = availableSeats <= totalSeats * 0.15 && availableSeats > 0;
                                    bool isSoldOut = availableSeats == 0;
                                    
                                    // Log for debugging
                                    if (isAlmostFull)
                                    {
                                        _logger.LogInformation("Found almost full presentation: ID {PresentationId}, Movie: {Title}, Available: {Available}/{Total} ({Percentage:F1}%)",
                                            p.Id, p.Movie.Title, availableSeats, totalSeats, (double)availableSeats / totalSeats * 100);
                                    }
                                    
                                    if (isSoldOut)
                                    {
                                        _logger.LogInformation("Found sold out presentation: ID {PresentationId}, Movie: {Title}, Available: 0/{Total}",
                                            p.Id, p.Movie.Title, totalSeats);
                                    }
                                    
                                    return new
                                    {
                                        p.Id,
                                        p.HallId,
                                        HallName = p.Hall.Name,
                                        StartTime = p.StartTime.ToString("HH:mm"),
                                        EndTime = p.EndTime.ToString("HH:mm"),
                                        p.Format,
                                        p.Price,
                                        TotalSeats = totalSeats,
                                        AvailableSeats = availableSeats,
                                        IsAlmostFull = isAlmostFull,
                                        IsSoldOut = isSoldOut
                                    };
                                }).ToList()
                            }).ToList()
                    }).ToList();

                // Store in cache for future requests
                _cache.Set(cacheKey, groupedByDate, _cacheDuration);
                _logger.LogInformation("Added schedule to cache with key: {CacheKey}", cacheKey);

                return Ok(groupedByDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving schedule");
                return StatusCode(500, "An error occurred while retrieving the schedule");
            }
        }
        
        /// <summary>
        /// Get presentations for a specific movie by date range
        /// </summary>
        /// <param name="movieId">Movie ID</param>
        /// <param name="startDate">Optional start date (default: today)</param>
        /// <param name="endDate">Optional end date (default: 7 days from start date)</param>
        /// <param name="format">Optional format filter</param>
        /// <returns>Movie details with available showtimes</returns>
        [HttpGet("movie/{movieId}")]
        public async Task<ActionResult<object>> GetMovieSchedule(
            int movieId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? format = null)
        {
            try
            {
                // Set default date range if not provided
                var actualStartDate = startDate ?? DateTime.Today;
                var actualEndDate = endDate ?? actualStartDate.AddDays(7);

                // Validate date range
                if (actualEndDate < actualStartDate)
                {
                    return BadRequest("End date cannot be earlier than start date");
                }

                // Create cache key based on parameters
                string cacheKey = $"movie_schedule_{movieId}_{actualStartDate:yyyyMMdd}_{actualEndDate:yyyyMMdd}_{format}";

                // Try to get from cache first
                if (_cache.TryGetValue(cacheKey, out object cachedSchedule))
                {
                    _logger.LogInformation("Retrieved movie schedule from cache with key: {CacheKey}", cacheKey);
                    return Ok(cachedSchedule);
                }

                // Get movie details
                var movie = await _context.Movies
                    .Include(m => m.Formats)
                    .FirstOrDefaultAsync(m => m.Id == movieId);

                if (movie == null)
                {
                    return NotFound("Movie not found");
                }

                // Get presentations for this movie in the date range
                var query = _context.Presentations
                    .Include(p => p.Movie)
                    .Include(p => p.Hall)
                    .Include(p => p.Tickets.Where(t => t.Status != TicketStatus.Cancelled))
                    .Where(p => p.MovieId == movieId &&
                                p.StartTime.Date >= actualStartDate.Date &&
                                p.StartTime.Date <= actualEndDate.Date);

                // Apply format filter if provided
                if (!string.IsNullOrEmpty(format))
                {
                    query = query.Where(p => p.Format.ToLower() == format.ToLower());
                }

                // Execute query and get presentations
                var presentations = await query.ToListAsync();

                // Calculate available seats and group by date
                var groupedByDate = presentations
                    .Select(p => new
                    {
                        p.Id,
                        p.HallId,
                        HallName = p.Hall.Name,
                        p.StartTime,
                        p.EndTime,
                        p.Format,
                        p.Price,
                        TotalSeats = p.Hall.Rows * p.Hall.SeatsPerRow,
                        AvailableSeats = (p.Hall.Rows * p.Hall.SeatsPerRow) - p.Tickets.Count(t => t.Status != TicketStatus.Cancelled),
                        Date = p.StartTime.Date
                    })
                    .OrderBy(p => p.StartTime)
                    .GroupBy(p => p.Date)
                    .Select(g => new
                    {
                        Date = g.Key.ToString("yyyy-MM-dd"),
                        DayOfWeek = g.Key.DayOfWeek.ToString(),
                        Showtimes = g.Select(p => new
                        {
                            p.Id,
                            p.HallId,
                            p.HallName,
                            StartTime = p.StartTime.ToString("HH:mm"),
                            EndTime = p.EndTime.ToString("HH:mm"),
                            p.Format,
                            p.Price,
                            p.TotalSeats,
                            p.AvailableSeats,
                            IsAlmostFull = p.AvailableSeats <= p.TotalSeats * 0.15 && p.AvailableSeats > 0, // Less than 15% seats available
                            IsSoldOut = p.AvailableSeats == 0
                        }).ToList()
                    })
                    .OrderBy(g => g.Date)
                    .ToList();

                // Create response with movie details and schedule
                var result = new
                {
                    movie.Id,
                    movie.Title,
                    movie.Description,
                    movie.DurationMinutes,
                    movie.Genre,
                    movie.AgeRating,
                    movie.PosterUrl,
                    movie.BackdropUrl,
                    Formats = movie.Formats.Select(f => new { f.Name, f.Description }).ToList(),
                    Schedule = groupedByDate
                };

                // Store in cache for future requests
                _cache.Set(cacheKey, result, _cacheDuration);
                _logger.LogInformation("Added movie schedule to cache with key: {CacheKey}", cacheKey);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving movie schedule for movie ID {MovieId}", movieId);
                return StatusCode(500, "An error occurred while retrieving the movie schedule");
            }
        }
    }
}