using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Models;
using WebApi.Repositories;
using Xunit;

namespace WebApi.Tests.Repositories
{
    public class TicketRepositoryTests
    {
        private readonly ApplicationDbContext _context;
        private readonly TicketRepository _repository;
        private readonly Mock<ILogger<TicketRepository>> _loggerMock;

        public TicketRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _loggerMock = new Mock<ILogger<TicketRepository>>();
            _repository = new TicketRepository(_context, _loggerMock.Object);

            SetupTestData();
        }

        private void SetupTestData()
        {
            // Clear existing data to avoid conflicts
            _context.Halls.RemoveRange(_context.Halls);
            _context.Seats.RemoveRange(_context.Seats);
            _context.Movies.RemoveRange(_context.Movies);
            _context.Presentations.RemoveRange(_context.Presentations);
            _context.SaveChanges();

            // Ensure hall is added
            var hall = new Hall
            {
                Id = 1,
                Name = "Test Hall",
                Rows = 10,
                SeatsPerRow = 15
            };
            _context.Halls.Add(hall);
            _context.SaveChanges(); // Save changes to ensure hall is persisted

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
            _context.SaveChanges(); // Save changes after adding seats

            var movie = new Movie { Id = 1, Title = "Test Movie" };
            _context.Movies.Add(movie);
            _context.SaveChanges(); // Save changes after adding movie

            // Add multiple presentations for better coverage
            for (int i = 0; i < 3; i++)
            {
                var presentation = new Presentation
                {
                    Id = i + 1,
                    MovieId = movie.Id,
                    Movie = movie,
                    HallId = hall.Id,
                    Hall = hall,
                    StartTime = DateTime.UtcNow.AddDays(i) // Different start times
                };
                _context.Presentations.Add(presentation);
            }
            _context.SaveChanges(); // Save changes after adding presentations
        }

        [Fact]
        public async Task GetAvailableSeats_WithBestSeatSelection_PrefersCenterBackSeats()
        {
            // Arrange
            var presentationId = 1;

            // Act
            var seats = await _repository.GetAvailableSeats(presentationId, findBestSeat: true);
            var bestSeat = seats.First();
            var hall = await _context.Halls.FirstAsync();

            // Assert
            // Should be around 2/3 back (row 7 in a 10-row theater)
            Assert.Equal((int)(hall.Rows * 0.66), bestSeat.RowNumber);
            // Should be in the middle
            Assert.Equal((int)Math.Ceiling(hall.SeatsPerRow / 2.0), bestSeat.SeatNumber);
        }

        [Fact]
        public async Task GetAvailableSeats_WithBestSeatSelection_AvoidsFrontRowCorners()
        {
            // Arrange
            var presentationId = 1;
            var hall = await _context.Halls.FirstAsync();
            var presentation = await _context.Presentations.FirstAsync();

            // Create a mock order for our test tickets
            var mockOrder = new TicketOrder
            {
                OrderToken = Guid.NewGuid(),
                PresentationId = presentationId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                Status = OrderStatus.Confirmed
            };
            _context.TicketOrders.Add(mockOrder);
            await _context.SaveChangesAsync();

            // Book all seats except front row corners and one middle seat
            var seatsToBook = await _context.Seats
                .Where(s => !(s.RowNumber == 1 && (s.SeatNumber == 1 || s.SeatNumber == hall.SeatsPerRow)) &&
                            !(s.RowNumber == 7 && s.SeatNumber == 8))
                .ToListAsync();

            foreach (var seat in seatsToBook)
            {
                _context.Tickets.Add(new Ticket
                {
                    PresentationId = presentationId,
                    SeatId = seat.Id,
                    TicketOrderId = mockOrder.Id,
                    Status = TicketStatus.Reserved,
                    CustomerName = "Test Customer",
                    CustomerEmail = "test@example.com",
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Presentation = presentation,
                    Seat = seat,
                    TicketOrder = mockOrder
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var seats = await _repository.GetAvailableSeats(presentationId, findBestSeat: true);
            var bestSeat = seats.First();

            // Assert
            Assert.True(bestSeat.RowNumber > 1, "Should avoid front row");
            Assert.True(bestSeat.SeatNumber > 3 && bestSeat.SeatNumber < 13, "Should avoid corner seats");
        }

        [Fact]
        public async Task GetAvailableSeats_WithBestSeatSelection_PrefersCenterOverSides()
        {
            // Arrange
            var presentationId = 1;
            var hall = await _context.Halls.FirstAsync();
            var presentation = await _context.Presentations.FirstAsync();

            // Create a mock order for our test tickets
            var mockOrder = new TicketOrder
            {
                OrderToken = Guid.NewGuid(),
                PresentationId = presentationId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                Status = OrderStatus.Confirmed
            };
            _context.TicketOrders.Add(mockOrder);
            await _context.SaveChangesAsync();

            // Book all center seats except row 7
            var seatsToBook = await _context.Seats
                .Where(s => s.SeatNumber >= 5 && s.SeatNumber <= 11 && s.RowNumber != 7)
                .ToListAsync();

            foreach (var seat in seatsToBook)
            {
                _context.Tickets.Add(new Ticket
                {
                    PresentationId = presentationId,
                    SeatId = seat.Id,
                    TicketOrderId = mockOrder.Id,
                    Status = TicketStatus.Reserved,
                    CustomerName = "Test Customer",
                    CustomerEmail = "test@example.com",
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Presentation = presentation,
                    Seat = seat,
                    TicketOrder = mockOrder
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var seats = await _repository.GetAvailableSeats(presentationId, findBestSeat: true);
            var bestSeat = seats.First();

            // Assert
            Assert.Equal(7, bestSeat.RowNumber);
            Assert.InRange(bestSeat.SeatNumber, 5, 11);
        }

        [Fact]
        public async Task GetAvailableSeats_WithCenterSeatsBookedExceptRow7_SelectsCenter()
        {
            // Arrange
            var presentationId = 1;
            var hall = await _context.Halls.FirstAsync();
            var presentation = await _context.Presentations.FirstAsync();

            // Create a mock order for our test tickets
            var mockOrder = new TicketOrder
            {
                OrderToken = Guid.NewGuid(),
                PresentationId = presentationId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                Status = OrderStatus.Confirmed
            };
            _context.TicketOrders.Add(mockOrder);
            await _context.SaveChangesAsync();

            // Book all center seats except row 7
            var seatsToBook = await _context.Seats
                .Where(s => s.SeatNumber >= 5 && s.SeatNumber <= 11 && s.RowNumber != 7)
                .ToListAsync();

            foreach (var seat in seatsToBook)
            {
                _context.Tickets.Add(new Ticket
                {
                    PresentationId = presentationId,
                    SeatId = seat.Id,
                    TicketOrderId = mockOrder.Id,
                    Status = TicketStatus.Reserved,
                    CustomerName = "Test Customer",
                    CustomerEmail = "test@example.com",
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Presentation = presentation,
                    Seat = seat,
                    TicketOrder = mockOrder
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var seats = await _repository.GetAvailableSeats(presentationId, findBestSeat: true);
            var bestSeat = seats.First();

            // Assert
            Assert.Equal(7, bestSeat.RowNumber);
            Assert.InRange(bestSeat.SeatNumber, 5, 11);
        }

        [Fact]
        public async Task GetAvailableSeats_WithFrontRowsBooked_SelectsNextBestSeats()
        {
            // Arrange
            var presentationId = 1;
            var presentation = await _context.Presentations.FirstAsync();

            // Create a mock order for our test tickets
            var mockOrder = new TicketOrder
            {
                OrderToken = Guid.NewGuid(),
                PresentationId = presentationId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                Status = OrderStatus.Confirmed
            };
            _context.TicketOrders.Add(mockOrder);
            await _context.SaveChangesAsync();

            // Book all seats except corners and front row
            var seatsToBook = await _context.Seats
                .Where(s => s.RowNumber > 1 &&
                           s.SeatNumber > 1 &&
                           s.SeatNumber < 15)
                .ToListAsync();

            foreach (var seat in seatsToBook)
            {
                _context.Tickets.Add(new Ticket
                {
                    PresentationId = presentationId,
                    SeatId = seat.Id,
                    TicketOrderId = mockOrder.Id,
                    Status = TicketStatus.Reserved,
                    CustomerName = "Test Customer",
                    CustomerEmail = "test@example.com",
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Presentation = presentation,
                    Seat = seat,
                    TicketOrder = mockOrder
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var seats = await _repository.GetAvailableSeats(presentationId, findBestSeat: true);

            // Assert
            Assert.NotEmpty(seats);
            var bestSeat = seats.First();

            // Should prefer middle seats in back rows over corner seats in front
            Assert.True(bestSeat.RowNumber > 1, "Should avoid front row");
            Assert.True(bestSeat.SeatNumber > 3 && bestSeat.SeatNumber < 13, "Should avoid corner seats");
        }

        [Fact]
        public async Task GetAvailableSeats_WithMostSeatsBooked_AvoidsCornerSeats()
        {
            // Arrange
            var presentationId = 1;
            var presentation = await _context.Presentations.FirstAsync();

            // Book all seats except corners and front row
            var seatsToBook = await _context.Seats
                .Where(s => s.RowNumber > 1 &&
                           s.SeatNumber > 1 &&
                           s.SeatNumber < 15)
                .ToListAsync();

            foreach (var seat in seatsToBook)
            {
                _context.Tickets.Add(new Ticket
                {
                    PresentationId = presentationId,
                    SeatId = seat.Id,
                    Status = TicketStatus.Reserved,
                    CustomerName = "Test Customer",
                    CustomerEmail = "test@example.com",
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Presentation = presentation,
                    Seat = seat
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var seats = await _repository.GetAvailableSeats(presentationId, findBestSeat: true);

            // Assert
            Assert.NotEmpty(seats);
            var bestSeat = seats.First();

            // Should prefer middle seats in back rows over corner seats in front
            Assert.True(bestSeat.RowNumber > 1, "Should avoid front row");
            Assert.True(bestSeat.SeatNumber > 3 && bestSeat.SeatNumber < 13, "Should avoid corner seats");
        }
    }
}