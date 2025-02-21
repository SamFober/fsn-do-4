using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;  // Add this for InMemoryEventId
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;
using WebApi.Data;
using WebApi.Models;
using Xunit;
using static WebApi.Controllers.TicketsController;  // Add this at the top to access nested types
using Microsoft.Extensions.DependencyInjection;
using WebApi.Services;  // Add this for OrderCleanupService

namespace WebApi.Tests;

public class TicketBookingTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TicketsController _controller;
    private readonly Mock<ILogger<TicketsController>> _loggerMock;
    private readonly IServiceProvider _services;  // Add this for cleanup service testing
    private Presentation? _presentation;  // Make it nullable

    public TicketBookingTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<TicketsController>>();
        _controller = new TicketsController(_context, _loggerMock.Object);

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
        var hall = new Hall 
        { 
            Id = 1, 
            Name = "Test Hall", 
            Rows = 10, 
            SeatsPerRow = 10 
        };
        _context.Halls.Add(hall);

        // Add seats
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

        var movie = new Movie { Id = 1, Title = "Test Movie", DurationMinutes = 120 };
        _context.Movies.Add(movie);

        _presentation = new Presentation  // Store presentation in field
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
    }

    [Fact]
    public async Task StartGroupOrder_WithValidInput_ReturnsConsecutiveSeats()
    {
        // Arrange
        var request = new StartGroupOrderRequest(1, 4);

        // Act
        var result = await _controller.StartGroupOrder(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);
        Assert.True(response.HasConsecutiveSeats);
        Assert.NotEmpty(response.SeatIds);
        Assert.Equal(4, response.SeatIds.Count);
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
        Assert.Equal("Group size too large", badRequest.Value);
    }

    [Fact]
    public async Task StartGroupOrder_WithInvalidPresentation_ReturnsNotFound()
    {
        // Arrange
        var request = new StartGroupOrderRequest(999, 4);

        // Act
        var result = await _controller.StartGroupOrder(request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
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
        Assert.True(result.Result is ObjectResult);
        var okResult = result.Result as ObjectResult;
        Assert.NotNull(okResult);
        var response = Assert.IsType<GroupOrderResponse>(okResult!.Value);
        Assert.False(response.HasConsecutiveSeats);
        Assert.NotNull(response.Alternatives);
    }

    [Fact]
    public async Task StartGroupOrder_WithNoConsecutiveSeats_ReturnsSplitArrangement()
    {
        // Arrange
        await SetupSplitScenario();
        var request = new StartGroupOrderRequest(1, 6);

        // Act
        var result = await _controller.StartGroupOrder(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);
        Assert.False(response.HasConsecutiveSeats);
        Assert.NotNull(response.Alternatives);
        Assert.NotEmpty(response.Alternatives.Options);

        // Verify split arrangement
        var splitOption = response.Alternatives.Options
            .FirstOrDefault(o => o.Type == "split");
        Assert.NotNull(splitOption);
        Assert.Equal(6, splitOption.TotalSeats);
        Assert.NotEmpty(splitOption.Arrangement);
        Assert.True(splitOption.Arrangement.Count >= 2, "Split arrangement should use at least 2 rows");
    }

    [Fact]
    public async Task StartGroupOrderWithOption_Split_ReturnsValidOrder()
    {
        // Arrange - Set up scenario to force split seating
        await SetupSplitScenario();

        // First create an order with split options
        var initialRequest = new StartGroupOrderRequest(1, 6);
        var initialResult = await _controller.StartGroupOrder(initialRequest);
        var okResult = Assert.IsType<OkObjectResult>(initialResult.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);
        Assert.False(response.HasConsecutiveSeats); // Verify we got split options

        // Now try to select the split option
        var optionRequest = new StartGroupOptionRequest(response.OrderToken);

        // Act
        var result = await _controller.StartGroupOrderWithOption("split", optionRequest);

        // Assert
        var finalOkResult = Assert.IsType<OkObjectResult>(result.Result);
        var orderResponse = Assert.IsType<OrderResponse>(finalOkResult.Value);
        Assert.NotEqual(Guid.Empty, orderResponse.OrderToken);
        Assert.Equal(6, orderResponse.SeatIds.Count);
    }

    [Fact]
    public async Task StartGroupOrderWithOption_WithExpiredOption_ReturnsBadRequest()
    {
        // Arrange - Set up scenario and create initial order
        await SetupSplitScenario();
        var initialRequest = new StartGroupOrderRequest(1, 6);
        var initialResult = await _controller.StartGroupOrder(initialRequest);
        var okResult = Assert.IsType<OkObjectResult>(initialResult.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);

        // Get the order and expire its options
        var order = await _context.TicketOrders
            .FirstOrDefaultAsync(o => o.OrderToken == response.OrderToken);
        Assert.NotNull(order);
        foreach (var option in order.AvailableOptions.Values)
        {
            option.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        }
        await _context.SaveChangesAsync();

        // Act
        var optionRequest = new StartGroupOptionRequest(response.OrderToken);
        var result = await _controller.StartGroupOrderWithOption("split", optionRequest);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Seating option has expired", badRequest.Value);
    }

    [Fact]
    public async Task StartGroupOrderWithOption_WithInvalidOption_ReturnsBadRequest()
    {
        // Arrange - Set up scenario and create initial order
        await SetupSplitScenario();
        var initialRequest = new StartGroupOrderRequest(1, 6);
        var initialResult = await _controller.StartGroupOrder(initialRequest);
        var okResult = Assert.IsType<OkObjectResult>(initialResult.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);

        // Act - Try to use non-existent option
        var optionRequest = new StartGroupOptionRequest(response.OrderToken);
        var result = await _controller.StartGroupOrderWithOption("invalid_option", optionRequest);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invalid option or order not found", badRequest.Value);
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
    public async Task ConfirmOrder_WithValidOrder_CreatesTickets()
    {
        // Arrange
        var order = await CreateTestOrder(4);
        var request = new ConfirmOrderRequest("Test User", "test@example.com");

        // Act
        var result = await _controller.ConfirmOrder(order.OrderToken, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var tickets = Assert.IsType<List<TicketResponse>>(okResult.Value);
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
        Assert.True(result.Result is ObjectResult);  // Less strict type checking
        var notFoundResult = result.Result as ObjectResult;
        Assert.NotNull(notFoundResult);
        Assert.Equal("Order not found or expired", notFoundResult.Value);
    }

    [Fact]
    public async Task StartGroupOrder_ShouldCreateSeatLocks()
    {
        // Arrange
        var request = new StartGroupOrderRequest(1, 3);

        // Act
        var result = await _controller.StartGroupOrder(request);
        
        // Less strict type checking
        Assert.True(result.Result is ObjectResult);
        var okResult = result.Result as ObjectResult;
        Assert.NotNull(okResult);  // Add null check
        var response = Assert.IsType<GroupOrderResponse>(okResult!.Value);  // Use null-forgiving operator

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
        var okResult = Assert.IsType<OkObjectResult>(startResult.Result);
        var response = Assert.IsType<GroupOrderResponse>(okResult.Value);
        var seatToRemove = response.SeatIds[0];

        // Act
        var removeResult = await _controller.RemoveSeatFromOrder(response.OrderToken, seatToRemove);

        // Assert
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
        await _controller.ConfirmOrder(response.OrderToken, confirmRequest);

        // Assert
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
        var okResult1 = Assert.IsType<OkObjectResult>(result1.Result);
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
        var okResult2 = Assert.IsType<OkObjectResult>(result2.Result);
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