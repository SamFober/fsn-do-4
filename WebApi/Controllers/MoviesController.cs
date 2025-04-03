using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Interfaces.Services;
using WebApi.Models;

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
    [ProducesResponseType(typeof(Movie), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMovie(int id)
    {
        var movie = await _movieService.GetMovieById(id);

        if (movie == null)
        {
            return NotFound();
        }

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

    // Admin endpoints for managing movies
    [HttpPost]
    [ProducesResponseType(typeof(Movie), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMovie([FromBody] MovieCreateRequest request)
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest("Movie title is required");
            }

            // Create the new movie
            var movie = new Movie
            {
                Title = request.Title,
                Description = request.Description,
                DurationMinutes = request.DurationMinutes,
                ReleaseDate = request.ReleaseDate,
                Genre = request.Genre,
                AgeRating = request.AgeRating,
                PosterUrl = request.PosterUrl,
                TrailerUrl = request.TrailerUrl,
                BackdropUrl = string.Empty, // Default value
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _movieService.AddMovie(movie);

            _logger.LogInformation("Movie created: {Title} (ID: {Id})", movie.Title, movie.Id);

            return CreatedAtAction(nameof(GetMovie), new { id = movie.Id }, movie);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating movie");
            return StatusCode(500, "An error occurred while creating the movie");
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Movie), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMovie(int id, [FromBody] MovieUpdateRequest request)
    {
        try
        {
            var movie = await _movieService.GetMovieById(id);
            if (movie == null)
            {
                return NotFound($"Movie with ID {id} not found");
            }

            // Validation
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest("Movie title is required");
            }

            // Update movie properties
            movie.Title = request.Title;
            movie.Description = request.Description;
            movie.DurationMinutes = request.DurationMinutes;
            movie.ReleaseDate = request.ReleaseDate;
            movie.Genre = request.Genre;
            movie.AgeRating = request.AgeRating;
            movie.PosterUrl = request.PosterUrl;
            movie.TrailerUrl = request.TrailerUrl;
            movie.IsActive = request.IsActive;
            movie.UpdatedAt = DateTime.UtcNow;

            await _movieService.UpdateMovie(movie);

            _logger.LogInformation("Movie updated: {Title} (ID: {Id})", movie.Title, movie.Id);

            return Ok(movie);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating movie with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the movie");
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteMovie(int id)
    {
        try
        {
            var movie = await _movieService.GetMovieById(id);

            if (movie == null)
            {
                return NotFound($"Movie with ID {id} not found");
            }

            // Check if movie has any presentations
            if (movie.Presentations != null && movie.Presentations.Any())
            {
                // Option 1: Don't allow deletion if there are presentations
                return BadRequest("Cannot delete movie with existing presentations");
                
                // Option 2: (Alternative) Mark as inactive instead of deleting
                // movie.IsActive = false;
                // movie.UpdatedAt = DateTime.UtcNow;
                // await _context.SaveChangesAsync();
                // return Ok(new { message = "Movie marked as inactive due to existing presentations" });
            }

            await _movieService.DeleteMovie(movie);

            _logger.LogInformation("Movie deleted: {Title} (ID: {Id})", movie.Title, movie.Id);

            return Ok(new { message = "Movie deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting movie with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the movie");
        }
    }

    // DTOs for Movie operations
    public class MovieCreateRequest
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int DurationMinutes { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Genre { get; set; } = "";
        public string AgeRating { get; set; } = "";
        public string PosterUrl { get; set; } = "";
        public string TrailerUrl { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }

    public class MovieUpdateRequest : MovieCreateRequest
    {
        // Inherits all properties from MovieCreateRequest
    }
}
