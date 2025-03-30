using WebApi.Models;

namespace WebApi.Interfaces.Repositories
{
    public interface IMovieRepository
    {
        Task<List<Movie>> GetAllAsync();
        Task<Movie?> GetAsync(int id);
    }
}
