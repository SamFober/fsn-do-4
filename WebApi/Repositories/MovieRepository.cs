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
    }
}
