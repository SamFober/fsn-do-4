using WebApi.Interfaces.Repositories;
using WebApi.Interfaces.Services;
using WebApi.Models;

namespace WebApi.Services
{
    public class MovieService : IMovieService
    {
        private readonly IMovieRepository _movieRepository;
        private readonly ILogger<MovieService> _logger;

        public MovieService(IMovieRepository movieRepository, ILogger<MovieService> logger)
        {
            _movieRepository = movieRepository;
            _logger = logger;
        }

        public async Task<List<Movie>> GetAllMovies()
        {
            return await _movieRepository.GetAllAsync();
        }

        public async Task<Movie?> GetMovieById(int id)
        {
            return await _movieRepository.GetAsync(id);
        }

        public async Task<Movie> AddMovie(Movie movie)
        {
            // Ensure creation timestamps are set
            movie.CreatedAt = DateTime.UtcNow;
            movie.UpdatedAt = DateTime.UtcNow;
            
            return await _movieRepository.AddAsync(movie);
        }

        public async Task<Movie> UpdateMovie(Movie movie)
        {
            // Ensure update timestamp is set
            movie.UpdatedAt = DateTime.UtcNow;
            
            return await _movieRepository.UpdateAsync(movie);
        }

        public async Task DeleteMovie(Movie movie)
        {
            await _movieRepository.DeleteAsync(movie);
        }
    }
}
