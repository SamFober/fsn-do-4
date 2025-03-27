using WebApi.Models;

namespace WebApi.Interfaces.Services
{
    public interface IMovieService
    {
        Task<List<Movie>> GetAllMovies();
        Task<Movie?> GetMovieById(int id);
    }
}
