using WebApi.Models;

namespace WebApi.Interfaces.Services
{
    public interface IMovieService
    {
        Task<List<Movie>> GetAllMovies();
        Task<Movie?> GetMovieById(int id);
        Task<Movie> AddMovie(Movie movie);
        Task<Movie> UpdateMovie(Movie movie);
        Task DeleteMovie(Movie movie);
    }
}
