using WebApi.Interfaces.Repositories;
using WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Repositories
{
    public class ReviewRepository : IReviewRepository
    {
        private readonly ApplicationDbContext _context;

        public ReviewRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Review> CreateReview(Review review)
        {
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            return review;
        }

        public async Task<List<Review>> GetReviewsByMovieId(int movieId)
        {
            return await _context.Reviews.Where(r => r.MovieId == movieId).ToListAsync();
        }

        public async Task<Review> DeleteReview(Review review)
        {
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return review;
        }
    }
}
