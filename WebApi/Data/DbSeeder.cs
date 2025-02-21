using WebApi.Models;

namespace WebApi.Data
{
    public static class DbSeeder
    {
        public static async Task Initialize(ApplicationDbContext context)
        {
            // Only seed if the database is empty
            if (context.Halls.Any()) return;

            var halls = new[]
            {
                new Hall { Name = "IMAX 1", Rows = 15, SeatsPerRow = 20, CreatedAt = DateTime.UtcNow },
                new Hall { Name = "IMAX 2", Rows = 15, SeatsPerRow = 20, CreatedAt = DateTime.UtcNow },
                new Hall { Name = "Standard 1", Rows = 10, SeatsPerRow = 15, CreatedAt = DateTime.UtcNow },
                new Hall { Name = "Standard 2", Rows = 10, SeatsPerRow = 15, CreatedAt = DateTime.UtcNow },
                new Hall { Name = "VIP 1", Rows = 5, SeatsPerRow = 10, CreatedAt = DateTime.UtcNow },
                new Hall { Name = "VIP 2", Rows = 5, SeatsPerRow = 10, CreatedAt = DateTime.UtcNow }
            };

            foreach (var hall in halls)
            {
                // Create seats for each hall
                for (int row = 1; row <= hall.Rows; row++)
                {
                    for (int seatNum = 1; seatNum <= hall.SeatsPerRow; seatNum++)
                    {
                        var seat = new Seat
                        {
                            Hall = hall,  // Set the required Hall navigation property
                            RowNumber = row,
                            SeatNumber = seatNum,
                            CreatedAt = DateTime.UtcNow
                        };
                        hall.Seats.Add(seat);
                    }
                }
            }

            context.Halls.AddRange(halls);
            await context.SaveChangesAsync();

            // Add Movies
            var movies = new List<Movie>
            {
                new Movie 
                { 
                    Title = "Inception",
                    Description = "A thief who steals corporate secrets through dream-sharing technology is given the inverse task of planting an idea into the mind of a C.E.O.",
                    DurationMinutes = 148,
                    ReleaseDate = new DateTime(2010, 7, 16),
                    IsActive = true
                },
                new Movie 
                { 
                    Title = "The Dark Knight",
                    Description = "When the menace known as the Joker wreaks havoc and chaos on the people of Gotham, Batman must accept one of the greatest psychological and physical tests of his ability to fight injustice.",
                    DurationMinutes = 152,
                    ReleaseDate = new DateTime(2008, 7, 18),
                    IsActive = true
                }
            };
            context.Movies.AddRange(movies);
            await context.SaveChangesAsync();

            // Add Presentations (movie showings)
            var presentations = new List<Presentation>
            {
                new Presentation
                {
                    Movie = movies[0],
                    Hall = halls[0],
                    StartTime = DateTime.Now.Date.AddHours(18), // 6 PM today
                    EndTime = DateTime.Now.Date.AddHours(20).AddMinutes(28),
                    Price = 12.99m
                },
                new Presentation
                {
                    Movie = movies[1],
                    Hall = halls[1],
                    StartTime = DateTime.Now.Date.AddHours(20), // 8 PM today
                    EndTime = DateTime.Now.Date.AddHours(22).AddMinutes(32),
                    Price = 12.99m
                }
            };
            context.Presentations.AddRange(presentations);
            await context.SaveChangesAsync();
        }
    }
} 