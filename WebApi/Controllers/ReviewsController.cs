using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApi.Models;

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

        public async Task<IActionResult> DeleteReview([FromBody] Review review)
        {
            var deletedReview = await _reviewRepository.DeleteReview(review);
            return Ok(deletedReview);
        }
    }
}