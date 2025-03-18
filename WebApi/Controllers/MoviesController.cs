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
    public async Task< IActionResult > GetMovieById(int id)
    {
        var movie = await _context.Movies
            .Where(m => m.Id == id)
            .FirstOrDefaultAsync();

        if (movie == null)
            return NotFound();

        return Ok(movie);
    }
}
