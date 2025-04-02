using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Models.Requests;
using WebApi.Exceptions;
using WebApi.Interfaces.Services;
using WebApi.Models;
using WebApi.Models.Responses;
using WebApi.Interfaces.Repositories;

[Route("api/concessions")]
[ApiController]
public class ConcessionsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly IConcessionRepository _repository;
    private readonly ILogger<ConcessionsController> _logger;

    public ConcessionsController(ITicketService ticketService, IConcessionRepository concessionRepository, ILogger<ConcessionsController> logger)
    {
        _ticketService = ticketService;
        _repository = concessionRepository;
        _logger = logger;
    }

    // Add concession item to order
    [HttpPost("{orderToken}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> AddConcessionToOrder(
        Guid orderToken,
        [FromBody] AddConcessionRequest request)
    {
        try
        {
            var response = await _ticketService.AddConcessionToOrder(orderToken, request);
            return new OkObjectResult(response);
        }
        catch (OrderNotFoundException)
        {
            return NotFound("Order not found");
        }
        catch (ConcessionNotFoundException)
        {
            return NotFound("Concession not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding concession item to order");
            return StatusCode(500, "An error occurred");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetConcessionByID(int id)
    {
        var concession = await _repository.GetConcessionItemById(id);
        if (concession == null)
        {
            return NotFound();
        }
        return Ok(concession);
    }
    [HttpGet]
    public async Task<IActionResult> GetConcessions()
    {
        var concessions = await _repository.GetConcessionItems();
        return Ok(concessions);
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateConcession([FromBody] ConcessionItem concessionItem)
    {
        if (concessionItem == null)
        {
            return BadRequest("Concession item cannot be null.");
        }

        _repository.CreateConcession(concessionItem);

        // Return the created item with a 201 Created response
        return CreatedAtAction(nameof(GetConcessionByID), new { id = concessionItem.Id }, concessionItem);
    }
}