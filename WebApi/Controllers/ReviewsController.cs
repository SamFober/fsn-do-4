using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Interfaces.Repositories;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/review")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewRepository _reviewRepository;

        public ReviewsController(IReviewRepository reviewRepository)
        {
            _reviewRepository = reviewRepository;
        }

        [HttpPost]
        public async Task<IActionResult> CreateReview([FromBody] Review review)
        {
            if (review.Rating < 1 || review.Rating > 5)
                return BadRequest("Rating must be between 1 and 5.");

            var createdReview = await _reviewRepository.CreateReview(review);
            return Ok(createdReview);
        }

        [HttpGet("{movieId}")]
        public async Task<IActionResult> GetReviews(int movieId)
        {
            var reviews = await _reviewRepository.GetReviewsByMovieId(movieId);
            return Ok(reviews);
        }

        [HttpDelete("{reviewId}")]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var review = await _reviewRepository.GetReviewById(reviewId);
            if (review == null)
            {
                return NotFound();
            }

            await _reviewRepository.DeleteReview(review);
            return Ok(new {message = "Review succesfully deleted"});
        }   
    }
}