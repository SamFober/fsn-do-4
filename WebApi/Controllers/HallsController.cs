using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Models;
using System.Threading.Tasks;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HallsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HallsController> _logger;

        public record HallResponse(
            int Id,
            string Name,
            int Capacity,
            DateTime CreatedAt
        );

        public record CreateHallRequest
        {
            public string Name { get; set; } = "";
            public int Rows { get; set; }
            public int SeatsPerRow { get; set; }
            public bool IsActive { get; set; } = true;
            public string? Description { get; set; }
        }

        public record UpdateHallRequest
        {
            public string Name { get; set; } = "";
            public int Rows { get; set; }
            public int SeatsPerRow { get; set; }
            public bool IsActive { get; set; } = true;
            public string? Description { get; set; }
        }

        public HallsController(ApplicationDbContext context, ILogger<HallsController> logger)
        {
            _context = context;
            _logger = logger;
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
                    h.Rows,
                    h.SeatsPerRow,
                    h.IsActive,
                    h.Description,
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

            return Ok(new
            {
                hall.Id,
                hall.Name,
                Capacity = hall.Rows * hall.SeatsPerRow,
                hall.CreatedAt,
                hall.Rows,
                hall.SeatsPerRow,
                hall.IsActive,
                hall.Description,
                Seats = hall.Seats.Select(s => new
                {
                    s.Id,
                    s.RowNumber,
                    s.SeatNumber
                })
            });
        }

        [HttpPost]
        [ProducesResponseType(typeof(HallResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateHall([FromBody] CreateHallRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Hall name is required");
                }

                if (request.Rows <= 0 || request.SeatsPerRow <= 0)
                {
                    return BadRequest("Rows and seats per row must be greater than zero");
                }

                // Create new hall
                var hall = new Hall
                {
                    Name = request.Name,
                    Rows = request.Rows,
                    SeatsPerRow = request.SeatsPerRow,
                    IsActive = request.IsActive,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Halls.Add(hall);
                await _context.SaveChangesAsync();

                // Generate seats for the hall
                await GenerateSeatsForHall(hall);

                _logger.LogInformation("Hall created: {Name} (ID: {Id})", hall.Name, hall.Id);

                return CreatedAtAction(
                    nameof(GetHall),
                    new { id = hall.Id },
                    new HallResponse(
                        hall.Id,
                        hall.Name,
                        hall.Rows * hall.SeatsPerRow,
                        hall.CreatedAt
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating hall");
                return StatusCode(500, "An error occurred while creating the hall");
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(HallResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateHall(int id, [FromBody] UpdateHallRequest request)
        {
            try
            {
                var hall = await _context.Halls
                    .Include(h => h.Seats)
                    .FirstOrDefaultAsync(h => h.Id == id);

                if (hall == null)
                {
                    return NotFound($"Hall with ID {id} not found");
                }

                // Check if hall has presentations
                var hasPresentations = await _context.Presentations
                    .AnyAsync(p => p.HallId == id);

                // Don't allow changing rows/seats if hall has presentations
                if (hasPresentations && (hall.Rows != request.Rows || hall.SeatsPerRow != request.SeatsPerRow))
                {
                    return BadRequest("Cannot change hall dimensions because it has scheduled presentations");
                }

                // Validate request
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Hall name is required");
                }

                if (request.Rows <= 0 || request.SeatsPerRow <= 0)
                {
                    return BadRequest("Rows and seats per row must be greater than zero");
                }

                // Update hall
                hall.Name = request.Name;
                hall.IsActive = request.IsActive;
                hall.Description = request.Description;
                hall.UpdatedAt = DateTime.UtcNow;

                // If dimensions changed, regenerate seats
                bool dimensionsChanged = hall.Rows != request.Rows || hall.SeatsPerRow != request.SeatsPerRow;
                if (dimensionsChanged)
                {
                    hall.Rows = request.Rows;
                    hall.SeatsPerRow = request.SeatsPerRow;
                    
                    // Remove existing seats
                    _context.Seats.RemoveRange(hall.Seats);
                }

                await _context.SaveChangesAsync();

                // Regenerate seats if dimensions changed
                if (dimensionsChanged)
                {
                    await GenerateSeatsForHall(hall);
                }

                _logger.LogInformation("Hall updated: {Name} (ID: {Id})", hall.Name, hall.Id);

                return Ok(new HallResponse(
                    hall.Id,
                    hall.Name,
                    hall.Rows * hall.SeatsPerRow,
                    hall.CreatedAt
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating hall {HallId}", id);
                return StatusCode(500, "An error occurred while updating the hall");
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteHall(int id)
        {
            try
            {
                var hall = await _context.Halls
                    .Include(h => h.Seats)
                    .FirstOrDefaultAsync(h => h.Id == id);

                if (hall == null)
                {
                    return NotFound($"Hall with ID {id} not found");
                }

                // Check if hall has presentations
                var hasPresentations = await _context.Presentations
                    .AnyAsync(p => p.HallId == id);

                if (hasPresentations)
                {
                    return BadRequest("Cannot delete hall because it has scheduled presentations");
                }

                // Check if hall has any tickets associated with it (through presentations)
                var hasTickets = await _context.Tickets
                    .AnyAsync(t => t.Presentation.HallId == id);

                if (hasTickets)
                {
                    return BadRequest("Cannot delete hall because it has associated tickets");
                }

                // Remove all seats
                _context.Seats.RemoveRange(hall.Seats);

                // Remove the hall
                _context.Halls.Remove(hall);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Hall deleted: {Name} (ID: {Id})", hall.Name, hall.Id);

                return Ok(new { message = $"Hall '{hall.Name}' successfully deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting hall {HallId}", id);
                return StatusCode(500, "An error occurred while deleting the hall");
            }
        }

        // Helper method to generate seats for a hall
        private async Task GenerateSeatsForHall(Hall hall)
        {
            var seats = new List<Seat>();

            for (int row = 1; row <= hall.Rows; row++)
            {
                for (int seatNum = 1; seatNum <= hall.SeatsPerRow; seatNum++)
                {
                    seats.Add(new Seat
                    {
                        HallId = hall.Id,
                        Hall = hall,
                        RowNumber = row,
                        SeatNumber = seatNum,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.Seats.AddRangeAsync(seats);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Generated {SeatCount} seats for hall {HallName} (ID: {HallId})", 
                seats.Count, hall.Name, hall.Id);
        }
    }
}