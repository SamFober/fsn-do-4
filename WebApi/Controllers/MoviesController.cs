using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/movies")]
[ApiController]
public class MoviesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MoviesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMovieById(int id)
    {
        var movie = await _context.Movies
            .Include(m => m.Presentations) // Include presentations for the movie
            .Include(m => m.Formats)      // Include formats for the movie
            .Where(m => m.Id == id)
            .FirstOrDefaultAsync();

        if (movie == null) return NotFound();

        return Ok(movie);
    }
    [HttpGet]
    public async Task<IActionResult> GetMovies()
    {
        var movies = await _context.Movies
            .Include(m => m.Presentations) // Include presentations for each movie
            .Include(m => m.Formats)       // Include formats for each movie
            .Where(m => m.IsActive) // Optionally filter by active status
            .ToListAsync();

        return Ok(movies);
    }
}
