using WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Data
{
    public static class DbSeeder
    {
        public static async Task Initialize(ApplicationDbContext context)
        {
            // Check each entity type separately
            if (!await context.Halls.AnyAsync())
            {
                // Add halls
                var halls = new[]
                {
                    new Hall { Name = "Hall 1", Rows = 8, SeatsPerRow = 15 },
                    new Hall { Name = "Hall 2", Rows = 8, SeatsPerRow = 15 },
                    new Hall { Name = "Hall 3", Rows = 8, SeatsPerRow = 15 },
                    new Hall { Name = "Hall 4", Rows = 6, SeatsPerRow = 10 },
                    new Hall { Name = "Hall 5", Rows = 4, SeatsPerRow = 15 },
                    new Hall { Name = "Hall 6", Rows = 4, SeatsPerRow = 15 }
                };
                context.Halls.AddRange(halls);
                await context.SaveChangesAsync();

                // Add seats for each hall
                foreach (var hall in halls)
                {
                    var seats = new List<Seat>();
                    for (int row = 1; row <= hall.Rows; row++)
                    {
                        for (int seatNum = 1; seatNum <= hall.SeatsPerRow; seatNum++)
                        {
                            seats.Add(new Seat
                            {
                                Hall = hall,
                                RowNumber = row,
                                SeatNumber = seatNum,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                    context.Seats.AddRange(seats);
                }
                await context.SaveChangesAsync();
            }

            if (!await context.Movies.AnyAsync())
            {
                // Add sample movies
                var movies = new[]
                {
                    new Movie 
                    { 
                        Title = "The Matrix",
                        Description = "A computer programmer discovers a mysterious world.",
                        DurationMinutes = 136,
                        ReleaseDate = new DateTime(1999, 3, 31),
                        IsActive = true
                    },
                    new Movie 
                    { 
                        Title = "Inception",
                        Description = "A thief who steals corporate secrets through dream-sharing technology.",
                        DurationMinutes = 148,
                        ReleaseDate = new DateTime(2010, 7, 16),
                        IsActive = true
                    }
                };
                context.Movies.AddRange(movies);
                await context.SaveChangesAsync();
            }

            // Separate check for presentations
            if (!await context.Presentations.AnyAsync())
            {
                var movies = await context.Movies.Take(2).ToListAsync();
                var halls = await context.Halls.Take(2).ToListAsync();
                
                if (movies.Any() && halls.Any())
                {
                    var presentations = new[]
                    {
                        new Presentation
                        {
                            Movie = movies[0],
                            Hall = halls[0],
                            StartTime = DateTime.Today.AddHours(19),
                            EndTime = DateTime.Today.AddHours(21),
                            Price = 12.99m
                        },
                        new Presentation
                        {
                            Movie = movies[1],
                            Hall = halls[1],
                            StartTime = DateTime.Today.AddHours(20),
                            EndTime = DateTime.Today.AddHours(22),
                            Price = 14.99m
                        }
                    };
                    context.Presentations.AddRange(presentations);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
} 