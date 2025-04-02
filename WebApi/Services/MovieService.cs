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
    }
}
