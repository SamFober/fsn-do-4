using WebApi.Models;

namespace WebApi.Interfaces.Repositories
{
    public interface IMovieRepository
    {
        Task<List<Movie>> GetAllAsync();
        Task<Movie?> GetAsync(int id);
        Task<Movie> AddAsync(Movie movie);
        Task<Movie> UpdateAsync(Movie movie);
        Task DeleteAsync(Movie movie);
    }
}
