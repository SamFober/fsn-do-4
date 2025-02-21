using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Models;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PresentationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PresentationsController(ApplicationDbContext context)
        {
            _context = context;
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
                    .ThenInclude(h => h.Seats)
                .Include(p => p.Tickets)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (presentation == null)
            {
                return NotFound();
            }

            var occupiedSeatIds = presentation.Tickets
                .Where(t => t.Status != TicketStatus.Cancelled)
                .Select(t => t.SeatId)
                .ToHashSet();

            var seatAvailability = presentation.Hall.Seats
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .Select(s => new
                {
                    s.Id,
                    s.RowNumber,
                    s.SeatNumber,
                    IsAvailable = !occupiedSeatIds.Contains(s.Id)
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
    }
} 