using WebApi.Models;

namespace WebApi.Interfaces.Repositories
{
    public interface IReviewRepository
    {
        Task<Review> CreateReview(Review review);
        Task<List<Review>> GetReviewsByMovieId(int movieId);
        Task<Review> DeleteReview(Review review);
    }
}

