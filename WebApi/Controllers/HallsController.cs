using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HallsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public record HallResponse(
            int Id,
            string Name,
            int Capacity,
            DateTime CreatedAt
        );

        public HallsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetHalls()
        {
            var halls = await _context.Halls
                .Include(h => h.Seats)
                .Select(h => new
                {
                    h.Id,
                    h.Name,
                    Capacity = h.Rows * h.SeatsPerRow,  // Calculate capacity
                    h.CreatedAt,
                    Seats = h.Seats.Select(s => new
                    {
                        s.Id,
                        s.RowNumber,
                        s.SeatNumber
                    })
                })
                .ToListAsync();

            return Ok(halls);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<HallResponse>> GetHall(int id)
        {
            var hall = await _context.Halls
                .Include(h => h.Seats)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (hall == null)
            {
                return NotFound();
            }

            return Ok(new HallResponse(
                hall.Id,
                hall.Name,
                hall.Rows * hall.SeatsPerRow,  // Calculate capacity
                hall.CreatedAt
            ));
        }
    }
}