using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Interfaces.Services;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MoviesController : ControllerBase
{
    private readonly IMovieService _movieService;
    private readonly ILogger<MoviesController> _logger;

    public MoviesController(IMovieService movieService, ILogger<MoviesController> logger)
    {
        _movieService = movieService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMovieById(int id)
    {
        var movie = await _movieService.GetMovieById(id);

        if (movie == null) return NotFound();

        return Ok(movie);
    }
    [HttpGet]
    public async Task<IActionResult> GetMovies()
    {
        var movies = await _movieService.GetAllMovies();

        return Ok(movies);
    }

    public async Task<IActionResult> GetMovies([FromQuery] string? search, [FromQuery] string? genre)
    {
        var movies = await _movieService.GetAllMovies();

        if (!string.IsNullOrEmpty(search))
        {
            movies = movies.Where(m => m.Title.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(genre))
        {
            movies = movies.Where(m => m.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // If no movies match, return the full list instead of an empty list
        if (!movies.Any())
        {
            movies = await _movieService.GetAllMovies(); // Reset to all movies
        }

        return Ok(movies);
    }

}
