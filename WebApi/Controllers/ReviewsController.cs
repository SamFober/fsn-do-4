using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Models;

[ApiController]
[Route("api/review")]
public class ReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReviewsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateReview([FromBody] Review review)
    {
        if (review.Rating < 1 || review.Rating > 5)
            return BadRequest("Rating must be between 1 and 5.");

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        return Ok(review);
    }

    [HttpGet("{movieId}")]
    public async Task<IActionResult> GetReviews(int movieId)
    {
        var reviews = await _context.Reviews.Where(r => r.MovieId == movieId).ToListAsync();
        return Ok(reviews);
    }
}
