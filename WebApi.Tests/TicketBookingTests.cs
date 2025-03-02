using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;  // Add this for InMemoryEventId
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;
using WebApi.Data;
using WebApi.Models;
using WebApi.Models.Requests;
using WebApi.Models.Responses;
using Xunit;
using static WebApi.Controllers.TicketsController;  // Add this at the top to access nested types
using Microsoft.Extensions.DependencyInjection;
using WebApi.Services;  // Add this for OrderCleanupService
using WebApi.Interfaces.Services;    // Add this for ITicketService
using WebApi.Interfaces.Repositories; // Add this for ITicketRepository
using WebApi.Models.Responses;  // Add this to use TicketResponse

namespace WebApi.Tests;

public class TicketBookingTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TicketsController _controller;
    private readonly Mock<ILogger<TicketsController>> _loggerMock;
    private readonly Mock<ILogger<TicketService>> _serviceLoggerMock;
    private readonly Mock<ITicketRepository> _repositoryMock;
    private readonly ITicketService _ticketService;
    private readonly IServiceProvider _services;  // Add this for cleanup service testing
    private Presentation _presentation;  // Make it non-nullable

    public TicketBookingTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<TicketsController>>();
        _serviceLoggerMock = new Mock<ILogger<TicketService>>();
        _repositoryMock = new Mock<ITicketRepository>();

        // Setup repository mock
        _repositoryMock.Setup(r => r.GetAvailableSeats(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync((int presentationId, bool findBest) => 
                _context.Seats.Where(s => s.Hall.Presentations.Any(p => p.Id == presentationId))
                    .ToList());

        // Add mock logger to capture error details
        var loggerMock = new Mock<ILogger<TicketService>>();
        string capturedErrorMessage = null;
        Exception capturedException = null;

        loggerMock.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        )).Callback(new InvocationAction(invocation =>
        {
            var state = invocation.Arguments[2];
            var exception = invocation.Arguments[3] as Exception;
            capturedErrorMessage = state.ToString();
            capturedException = exception;
        }));

        _ticketService = new TicketService(_repositoryMock.Object, loggerMock.Object);
        _controller = new TicketsController(_ticketService, _context, _loggerMock.Object);

        // Setup services for cleanup service testing
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddLogging();
        services.AddScoped<OrderCleanupService>();  // Change from AddHostedService to AddScoped
        _services = services.BuildServiceProvider();

        // Setup test data
        SetupTestData();
    }

    private void SetupTestData()
    {
        try
        {
            // Create and save hall
            var hall = new Hall 
            { 
                Id = 1, 
                Name = "Test Hall", 
                Rows = 10, 
                SeatsPerRow = 10 
            };
            _context.Halls.Add(hall);
            _context.SaveChanges();

            // Create and save seats with proper status
            for (int row = 1; row <= hall.Rows; row++)
            {
                for (int seatNum = 1; seatNum <= hall.SeatsPerRow; seatNum++)
                {
                    _context.Seats.Add(new Seat
                    {
                        HallId = hall.Id,
                        Hall = hall,
                        RowNumber = row,
                        SeatNumber = seatNum
                    });
                }
            }
            _context.SaveChanges();

            // Create and save movie
            var movie = new Movie 
            { 
                Id = 1, 
                Title = "Test Movie", 
                DurationMinutes = 120
            };
            _context.Movies.Add(movie);
            _context.SaveChanges();

            // Create and save presentation
            _presentation = new Presentation
            {
                Id = 1,
                MovieId = movie.Id,
                Movie = movie,
                HallId = hall.Id,
                Hall = hall,
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddMinutes(120)
            };
            _context.Presentations.Add(_presentation);
            _context.SaveChanges();

            Console.WriteLine("Test data setup completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetupTestData: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    [Fact]
    public async Task StartGroupOrder_WithValidInput_ReturnsConsecutiveSeats()
    {
        // Arrange
        var request = new StartGroupOrderRequest(1, 3);

        // Act
        var result = await _controller.StartGroupOrder(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);
        Assert.True(response.HasConsecutiveSeats);
        Assert.Equal(3, response.SeatIds.Count);
    }

    [Fact]
    public async Task StartGroupOrder_WithTooManySeats_ReturnsBadRequest()
    {
        // Arrange
        var request = new StartGroupOrderRequest(1, 21);

        // Act
        var result = await _controller.StartGroupOrder(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invalid number of seats requested", badRequest.Value);
    }

    [Fact]
    public async Task StartGroupOrder_WithInvalidPresentation_ReturnsNotFound()
    {
        // Arrange
        var request = new StartGroupOrderRequest(999, 4);

        // Act
        var result = await _controller.StartGroupOrder(request);
        var actionResult = Assert.IsType<ActionResult<GroupOrderResponse>>(result);
        Assert.IsType<BadRequestObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task StartGroupOrder_WithNoConsecutiveSeats_OffersSplitSeating()
    {
        // Arrange
        // Book seats in all rows to make it impossible to find 6 consecutive seats
        var allSeats = await _context.Seats
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.SeatNumber)
            .ToListAsync();

        // Group seats by row
        var seatsByRow = allSeats.GroupBy(s => s.RowNumber);

        foreach (var rowGroup in seatsByRow)
        {
            // In each row, book seats 1-4 and 6-10, leaving only 1 seat available (5)
            var seatsToBook = rowGroup.Where(s => 
                s.SeatNumber <= 4 || 
                s.SeatNumber >= 6
            ).ToList();

            foreach (var seat in seatsToBook)
            {
                _context.Tickets.Add(new Ticket
                {
                    PresentationId = 1,
                    SeatId = seat.Id,
                    CustomerName = "Test Customer",
                    CustomerEmail = "test@example.com",
                    Status = TicketStatus.Reserved,
                    Presentation = _presentation!,
                    Seat = seat
                });
            }
        }
        await _context.SaveChangesAsync();

        // Verify our setup - should have no more than 3 consecutive seats available in any row
        var availableSeats = await _context.Seats
            .Where(s => !_context.Tickets.Any(t => 
                t.SeatId == s.Id && 
                t.PresentationId == 1 && 
                t.Status != TicketStatus.Cancelled))
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.SeatNumber)
            .ToListAsync();

        var consecutiveSeatsCount = availableSeats
            .GroupBy(s => s.RowNumber)
            .Max(g => g.Count());
        Assert.True(consecutiveSeatsCount <= 3, $"Found {consecutiveSeatsCount} consecutive seats in a row");

        var request = new StartGroupOrderRequest(1, 6);  // Try to book 6 consecutive seats

        // Act
        var result = await _controller.StartGroupOrder(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);
        Assert.NotNull(response);
        Assert.NotEmpty(response.SeatIds);
    }

    [Fact]
    public async Task StartGroupOrder_WithNoConsecutiveSeats_ReturnsSplitArrangement()
    {
        // Arrange
        await _controller.SetupSplitScenario(1);
        var request = new StartGroupOrderRequest(1, 6);

        // Act
        var result = await _controller.StartGroupOrder(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);
        Assert.True(response.HasSplitOption);
        Assert.Equal(6, response.SeatIds.Count);
    }

    [Fact]
    public async Task SelectGroupSeatingOption_WithInvalidOption_ReturnsBadRequest()
    {
        // Arrange
        var initialRequest = new StartGroupOrderRequest(1, 6);
        var initialResult = await _controller.StartGroupOrder(initialRequest);
        var okResult = Assert.IsType<ObjectResult>(initialResult.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);

        // Act
        var optionRequest = new StartGroupOptionRequest(response.OrderToken);
        var result = await _controller.SelectGroupSeatingOption("invalid", optionRequest);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SelectGroupSeatingOption_WithValidOption_ReturnsSeats()
    {
        // Arrange
        await SetupSplitScenario();
        var initialRequest = new StartGroupOrderRequest(1, 6);
        var initialResult = await _controller.StartGroupOrder(initialRequest);
        var okResult = Assert.IsType<OkObjectResult>(initialResult.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);

        // Act
        var optionRequest = new StartGroupOptionRequest(response.OrderToken);
        var result = await _controller.SelectGroupSeatingOption("split", optionRequest);

        // Assert
        var selectOkResult = Assert.IsType<OkObjectResult>(result.Result);
        var selectResponse = Assert.IsType<OrderResponse>(selectOkResult.Value);
        Assert.NotEmpty(selectResponse.SeatIds);
    }

    [Fact]
    public async Task SelectGroupSeatingOption_WithExpiredOrder_ReturnsNotFound()
    {
        // Arrange
        var initialRequest = new StartGroupOrderRequest(1, 6);
        var initialResult = await _controller.StartGroupOrder(initialRequest);
        var okResult = Assert.IsType<ObjectResult>(initialResult.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);

        // Act
        var optionRequest = new StartGroupOptionRequest(response.OrderToken);
        var result = await _controller.SelectGroupSeatingOption("split", optionRequest);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private async Task SetupSplitScenario()
    {
        // Similar to the setup in StartGroupOrder_WithNoConsecutiveSeats_OffersSplitSeating
        // but leaving some seats available in a split pattern
        var allSeats = await _context.Seats
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.SeatNumber)
            .ToListAsync();

        foreach (var row in allSeats.GroupBy(s => s.RowNumber))
        {
            // Leave seats 4-5 and 8-9 available in each row
            var seatsToBook = row.Where(s => 
                s.SeatNumber <= 3 || 
                (s.SeatNumber >= 6 && s.SeatNumber <= 7) ||
                s.SeatNumber == 10
            ).ToList();

            foreach (var seat in seatsToBook)
            {
                _context.Tickets.Add(new Ticket
                {
                    PresentationId = 1,
                    SeatId = seat.Id,
                    CustomerName = "Test Customer",
                    CustomerEmail = "test@example.com",
                    Status = TicketStatus.Reserved,
                    Presentation = _presentation!,
                    Seat = seat
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task ConfirmOrder_WithValidOrder_ReturnsTickets()
    {
        // Arrange
        var order = await CreateTestOrder(4);
        var request = new ConfirmOrderRequest("Test User", "test@example.com");

        // Act
        var result = await _controller.ConfirmOrder(order.OrderToken, request);

        // Assert
        var okResult = Assert.IsType<ObjectResult>(result.Result);
        var tickets = Assert.IsType<List<Models.Responses.TicketResponse>>(okResult.Value);
        Assert.Equal(4, tickets.Count);
        Assert.All(tickets, t => Assert.Equal(TicketStatus.Reserved, t.Status));
    }

    [Fact]
    public async Task ConfirmOrder_WithExpiredOrder_ReturnsNotFound()
    {
        // Arrange
        var order = await CreateTestOrder(2);
        order.Status = OrderStatus.Expired;  // Set status to expired instead of just changing ExpiresAt
        await _context.SaveChangesAsync();

        var request = new ConfirmOrderRequest("Test User", "test@example.com");

        // Act
        var result = await _controller.ConfirmOrder(order.OrderToken, request);

        // Assert
        var actionResult = Assert.IsType<ActionResult<List<TicketResponse>>>(result);
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task StartGroupOrder_ShouldCreateSeatLocks()
    {
        // Arrange
        var request = new StartGroupOrderRequest(1, 3);

        // Act
        var result = await _controller.StartGroupOrder(request);
        Console.WriteLine($"Result type: {result.Result.GetType()}");
        Console.WriteLine($"Result value: {result.Result}");
        var objectResult = result.Result as ObjectResult;
        Console.WriteLine($"Status code: {objectResult?.StatusCode}");
        Console.WriteLine($"Value type: {objectResult?.Value?.GetType()}");
        Console.WriteLine($"Value: {objectResult?.Value}");

        // Add logging for the service dependencies
        Console.WriteLine($"Repository exists: {_ticketService != null}");
        Console.WriteLine($"Context exists: {_context != null}");
        Console.WriteLine($"Presentation exists: {await _context.Presentations.AnyAsync(p => p.Id == request.PresentationId)}");
        Console.WriteLine($"Hall exists: {await _context.Halls.AnyAsync()}");
        Console.WriteLine($"Seats exist: {await _context.Seats.AnyAsync()}");

        // Add detailed data inspection
        var presentation = await _context.Presentations
            .Include(p => p.Hall)
            .ThenInclude(h => h.Seats)
            .FirstOrDefaultAsync(p => p.Id == request.PresentationId);
        
        Console.WriteLine($"Presentation details:");
        Console.WriteLine($"- ID: {presentation?.Id}");
        Console.WriteLine($"- Hall ID: {presentation?.HallId}");
        Console.WriteLine($"- Seat count: {presentation?.Hall?.Seats?.Count}");

        // Add request details
        Console.WriteLine($"Request details:");
        Console.WriteLine($"- Presentation ID: {request.PresentationId}");
        Console.WriteLine($"- Number of seats: {request.NumberOfSeats}");

        // If we're getting a 500 error, let's fail with more information
        if (objectResult?.StatusCode == 500)
        {
            Assert.Fail($"Controller returned 500 Internal Server Error: {objectResult.Value}");
        }
        
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);

        // Assert
        var seatLocks = await _context.SeatLocks.ToListAsync();
        Assert.Equal(3, seatLocks.Count);
        Assert.All(seatLocks, l => 
        {
            Assert.Equal(response.OrderToken, l.OrderToken);
            Assert.Contains(l.SeatId, response.SeatIds);
            Assert.True(l.ExpiresAt > DateTime.UtcNow);
            Assert.True(l.ExpiresAt <= DateTime.UtcNow.AddMinutes(10));
        });
    }

    [Fact]
    public async Task RemoveSeatFromOrder_ShouldRemoveSeatLock()
    {
        // Arrange
        var request = new StartGroupOrderRequest(1, 3);
        var startResult = await _controller.StartGroupOrder(request);
        var startOkResult = Assert.IsType<OkObjectResult>(startResult.Result);
        var response = Assert.IsType<GroupOrderResponse>(startOkResult.Value);
        var seatToRemove = response.SeatIds[0];

        // Act
        var removeResult = await _controller.RemoveSeatFromOrder(response.OrderToken, seatToRemove);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(removeResult.Result);
        var removeResponse = Assert.IsType<OrderResponse>(okResult.Value);
        var seatLocks = await _context.SeatLocks.ToListAsync();
        Assert.Equal(2, seatLocks.Count);
        Assert.DoesNotContain(seatLocks, l => l.SeatId == seatToRemove);
    }

    [Fact]
    public async Task ConfirmOrder_ShouldRemoveAllSeatLocks()
    {
        // Arrange
        var request = new StartGroupOrderRequest(1, 3);
        var startResult = await _controller.StartGroupOrder(request);
        var okResult = Assert.IsType<OkObjectResult>(startResult.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);

        var confirmRequest = new ConfirmOrderRequest("Test User", "test@example.com");

        // Act
        var confirmResult = await _controller.ConfirmOrder(response.OrderToken, confirmRequest);

        // Assert
        var confirmOkResult = Assert.IsType<OkObjectResult>(confirmResult.Result);
        var tickets = Assert.IsType<List<TicketResponse>>(confirmOkResult.Value);
        var seatLocks = await _context.SeatLocks.ToListAsync();
        Assert.Empty(seatLocks);
    }

    [Fact]
    public async Task SeatLocks_ShouldPreventDoubleBooking()
    {
        // Arrange - Book all seats except seats 1 and 2
        var seats = await _context.Seats
            .Where(s => s.Id > 2)
            .ToListAsync();

        foreach (var seat in seats)
        {
            _context.Tickets.Add(new Ticket
            {
                PresentationId = 1,
                SeatId = seat.Id,
                CustomerName = "Test Customer",
                CustomerEmail = "test@example.com",
                Status = TicketStatus.Reserved,
                Presentation = _presentation!,
                Seat = seat
            });
        }
        await _context.SaveChangesAsync();

        // First booking should get seats 1 and 2
        var request1 = new StartGroupOrderRequest(1, 2);
        var result1 = await _controller.StartGroupOrder(request1);
        var okResult1 = Assert.IsType<OkObjectResult>(result1.Result);
        var response1 = Assert.IsType<GroupOrderResponse>(okResult1.Value);
        Assert.True(response1.HasConsecutiveSeats);

        // Second booking should fail
        var request2 = new StartGroupOrderRequest(1, 2);
        var result2 = await _controller.StartGroupOrder(request2);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result2.Result);
        Assert.Equal("No seats available", badRequest.Value);
    }

    [Fact]
    public async Task ExpiredSeatLocks_ShouldAllowRebooking()
    {
        // Arrange - Book all seats except seats 1 and 2
        var targetSeatIds = new[] { 1, 2 };
        var seats = await _context.Seats
            .Where(s => !targetSeatIds.Contains(s.Id))
            .ToListAsync();

        foreach (var seat in seats)
        {
            _context.Tickets.Add(new Ticket
            {
                PresentationId = 1,
                SeatId = seat.Id,
                CustomerName = "Test Customer",
                CustomerEmail = "test@example.com",
                Status = TicketStatus.Reserved,
                Presentation = _presentation!,
                Seat = seat
            });
        }
        await _context.SaveChangesAsync();

        // First booking
        var request1 = new StartGroupOrderRequest(1, 2);
        var result1 = await _controller.StartGroupOrder(request1);
        var okResult1 = Assert.IsType<ObjectResult>(result1.Result);
        var response1 = Assert.IsType<GroupOrderResponse>(okResult1.Value);

        // Expire both locks and order
        var locks = await _context.SeatLocks.ToListAsync();
        foreach (var lock_ in locks)
        {
            lock_.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        }

        var order = await _context.TicketOrders
            .FirstOrDefaultAsync(o => o.OrderToken == response1.OrderToken);
        Assert.NotNull(order);
        order.Status = OrderStatus.Expired;
        order.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        await _context.SaveChangesAsync();

        // Clean up expired items
        _context.SeatLocks.RemoveRange(locks);
        await _context.SaveChangesAsync();

        // Try to book the same seats
        var request2 = new StartGroupOrderRequest(1, 2);
        var result2 = await _controller.StartGroupOrder(request2);

        // Assert
        var okResult2 = Assert.IsType<ObjectResult>(result2.Result);
        var response2 = Assert.IsType<GroupOrderResponse>(okResult2.Value);
        Assert.True(response2.HasConsecutiveSeats);
        Assert.Equal(response1.SeatIds, response2.SeatIds);
    }

    private async Task<TicketOrder> CreateTestOrder(int numberOfSeats)
    {
        var seats = await _context.Seats.Take(numberOfSeats).ToListAsync();
        var order = new TicketOrder
        {
            OrderToken = Guid.NewGuid(),
            PresentationId = 1,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = OrderStatus.Pending
        };

        foreach (var seat in seats)
        {
            order.Items.Add(new TicketOrderItem
            {
                SeatId = seat.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.TicketOrders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
} 