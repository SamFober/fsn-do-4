using Microsoft.EntityFrameworkCore;
using WebApi.Interfaces.Repositories;
using WebApi.Models;

namespace WebApi.Repositories
{
    public class MovieRepository : IMovieRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MovieRepository> _logger;

        public MovieRepository(ApplicationDbContext context, ILogger<MovieRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Movie>> GetAllAsync()
        {
            return await _context.Movies
            .Include(m => m.Presentations) // Include presentations for each movie
            .Include(m => m.Formats)       // Include formats for each movie
            .Where(m => m.IsActive) // Optionally filter by active status
            .ToListAsync();
        }

        public async Task<Movie?> GetAsync(int id)
        {
            return await _context.Movies
            .Include(m => m.Presentations) // Include presentations for the movie
            .Include(m => m.Formats)      // Include formats for the movie
            .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<Movie> AddAsync(Movie movie)
        {
            try
            {
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Movie {Title} created with ID {Id}", movie.Title, movie.Id);
                return movie;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding movie {Title}", movie.Title);
                throw;
            }
        }

        public async Task<Movie> UpdateAsync(Movie movie)
        {
            try
            {
                _context.Entry(movie).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Movie {Title} with ID {Id} updated", movie.Title, movie.Id);
                return movie;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating movie {Title} with ID {Id}", movie.Title, movie.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Movie movie)
        {
            try
            {
                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Movie {Title} with ID {Id} deleted", movie.Title, movie.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting movie {Title} with ID {Id}", movie.Title, movie.Id);
                throw;
            }
        }
    }
}
