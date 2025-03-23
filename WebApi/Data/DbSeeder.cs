using Microsoft.EntityFrameworkCore;
using WebApi.Models;
using static WebApi.Models.TicketStatus;

namespace WebApi.Data
{
    public static class DbSeeder
    {
        public static async Task Initialize(ApplicationDbContext context)
        {
            // Update existing presentations' AvailableSeats if needed
            await UpdatePresentationsAvailableSeats(context);
            
            // Seed halls
            if (!await context.Halls.AnyAsync())
            {
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
                                HallId = hall.Id, // Set HallId directly
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
                // Add movies
                var movies = new[]
                {
                    new Movie
                    {
                        Title = "The Matrix",
                        Description = "When a beautiful stranger leads computer hacker Neo to a forbidding underworld, he discovers the shocking truth--the life he knows is the elaborate deception of an evil cyber-intelligence.",
                        DurationMinutes = 136,
                        ReleaseDate = new DateTime(1999, 3, 31),
                        TrailerUrl = "https://www.youtube.com/embed/vKQi3bBA1y8",
                        PosterUrl = "https://m.media-amazon.com/images/M/MV5BNzQzOTk3OTAtNDQ0Zi00ZTVkLWI0MTEtMDllZjNkYzNjNTc4L2ltYWdlXkEyXkFqcGdeQXVyNjU0OTQ0OTY@._V1_.jpg",
                        BackdropUrl = "https://wallpapercave.com/wp/wp2030568.jpg",
                        Genre = "Sci-Fi/Action",
                        AgeRating = "PG-13",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Movie
                    {
                        Title = "Inception",
                        Description = "A thief who steals corporate secrets through the use of dream-sharing technology is given the inverse task of planting an idea into the mind of a C.E.O.",
                        DurationMinutes = 148,
                        ReleaseDate = new DateTime(2010, 7, 16),
                        TrailerUrl = "https://www.youtube.com/embed/YoHD9XEInc0",
                        PosterUrl = "https://m.media-amazon.com/images/M/MV5BMjAxMzY3NjcxNF5BMl5BanBnXkFtZTcwNTI5OTM0Mw@@._V1_.jpg",
                        BackdropUrl = "https://wallpaperaccess.com/full/1264682.jpg",
                        Genre = "Sci-Fi/Thriller",
                        AgeRating = "PG-13",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Movie
                    {
                        Title = "Interstellar",
                        Description = "A team of explorers travel through a wormhole in space in an attempt to ensure humanity's survival.",
                        DurationMinutes = 169,
                        ReleaseDate = new DateTime(2014, 11, 7),
                        TrailerUrl = "https://www.youtube.com/embed/zSWdZVtXT7E",
                        PosterUrl = "https://m.media-amazon.com/images/M/MV5BZjdkOTU3MDktN2IxOS00OGEyLWFmMjktY2FiMmZkNWIyODZiXkEyXkFqcGdeQXVyMTMxODk2OTU@._V1_.jpg",
                        BackdropUrl = "https://wallpapercave.com/wp/wp1817131.jpg",
                        Genre = "Sci-Fi/Adventure",
                        AgeRating = "PG-13",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Movie
                    {
                        Title = "The Dark Knight",
                        Description = "When the menace known as the Joker wreaks havoc and chaos on the people of Gotham, Batman must accept one of the greatest psychological and physical tests of his ability to fight injustice.",
                        DurationMinutes = 152,
                        ReleaseDate = new DateTime(2008, 7, 18),
                        TrailerUrl = "https://www.youtube.com/embed/EXeTwQWrcwY",
                        PosterUrl = "https://m.media-amazon.com/images/M/MV5BMTMxNTMwODM0NF5BMl5BanBnXkFtZTcwODAyMTk2Mw@@._V1_.jpg",
                        BackdropUrl = "https://wallpaperaccess.com/full/781011.jpg",
                        Genre = "Action/Crime",
                        AgeRating = "PG-13",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Movie
                    {
                        Title = "Pulp Fiction",
                        Description = "The lives of two mob hitmen, a boxer, a gangster and his wife, and a pair of diner bandits intertwine in four tales of violence and redemption.",
                        DurationMinutes = 154,
                        ReleaseDate = new DateTime(1994, 10, 14),
                        TrailerUrl = "https://www.youtube.com/embed/s7EdQ4FqbhY",
                        PosterUrl = "https://m.media-amazon.com/images/M/MV5BNGNhMDIzZTUtNTBlZi00MTRlLWFjM2ItYzViMjE3YzI5MjljXkEyXkFqcGdeQXVyNzkwMjQ5NzM@._V1_.jpg",
                        BackdropUrl = "https://wallpapercave.com/wp/wp3462462.jpg",
                        Genre = "Crime/Drama",
                        AgeRating = "R",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Movie
                    {
                        Title = "Avengers: Endgame",
                        Description = "After the devastating events of Avengers: Infinity War, the universe is in ruins. With the help of remaining allies, the Avengers assemble once more in order to reverse Thanos' actions and restore balance to the universe.",
                        DurationMinutes = 181,
                        ReleaseDate = new DateTime(2019, 4, 26),
                        TrailerUrl = "https://www.youtube.com/embed/TcMBFSGVi1c",
                        PosterUrl = "https://m.media-amazon.com/images/M/MV5BMTc5MDE2ODcwNV5BMl5BanBnXkFtZTgwMzI2NzQ2NzM@._V1_.jpg",
                        BackdropUrl = "https://wallpapercave.com/wp/wp4304504.jpg",
                        Genre = "Action/Sci-Fi",
                        AgeRating = "PG-13",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                context.Movies.AddRange(movies);
                await context.SaveChangesAsync();
                
                // Add movie formats
                if (!await context.MovieFormats.AnyAsync())
                {
                    var formats = new List<MovieFormat>();
                    
                    // Common formats for all movies
                    string[] formatNames = { "2D", "3D", "IMAX", "4DX", "Dolby Atmos", "ScreenX" };
                    string[] formatDescriptions = { 
                        "Standard 2D format", 
                        "Immersive 3D experience", 
                        "IMAX premium large-format experience",
                        "Full sensory immersion with motion and environmental effects",
                        "Enhanced audio experience",
                        "270-degree panoramic movie viewing experience"
                    };
                    string[] formatIcons = { "2d", "3d", "imax", "4dx", "dolby", "screenx" };
                    
                    // Assign 2-4 random formats to each movie
                    Random random = new Random(42); // Fixed seed for consistent results
                    
                    foreach (var movie in movies)
                    {
                        // Each movie gets at least 2D
                        formats.Add(new MovieFormat
                        {
                            MovieId = movie.Id,
                            Name = formatNames[0],
                            Description = formatDescriptions[0],
                            Icon = formatIcons[0],
                            CreatedAt = DateTime.UtcNow
                        });
                        
                        // Add 1-3 more random formats
                        int additionalFormats = random.Next(1, 4);
                        var availableFormats = Enumerable.Range(1, formatNames.Length - 1).ToList(); // Skip 2D which is already added
                        
                        for (int i = 0; i < additionalFormats && availableFormats.Count > 0; i++)
                        {
                            int formatIndex = availableFormats[random.Next(availableFormats.Count)];
                            availableFormats.Remove(formatIndex);
                            
                            formats.Add(new MovieFormat
                            {
                                MovieId = movie.Id,
                                Name = formatNames[formatIndex],
                                Description = formatDescriptions[formatIndex],
                                Icon = formatIcons[formatIndex],
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                    
                    context.MovieFormats.AddRange(formats);
                    await context.SaveChangesAsync();
                }

                // Add presentations
                if (!await context.Presentations.AnyAsync())
                {
                    var existingMovies = await context.Movies.ToListAsync();
                    var halls = await context.Halls.ToListAsync();

                    if (existingMovies.Any() && halls.Any())
                    {
                        var presentations = new List<Presentation>();
                        var random = new Random(42); // Fixed seed for consistent results
                        
                        // Create presentations for the next 14 days
                        for (int day = 0; day < 14; day++)
                        {
                            DateTime currentDate = DateTime.Today.AddDays(day);
                            
                            // Each hall gets 3-5 showings per day
                            foreach (var hall in halls)
                            {
                                int showingsCount = random.Next(3, 6);
                                
                                // Starting times between 10:00 and 22:00
                                var startTimes = new List<DateTime>();
                                for (int i = 0; i < showingsCount; i++)
                                {
                                    // Start times at 10:00, 13:00, 16:00, 19:00, 22:00
                                    int hour = 10 + (i * 3);
                                    if (hour > 22) continue;
                                    
                                    // Add some randomness to the minutes
                                    int minutes = random.Next(0, 4) * 15; // 0, 15, 30, or 45 minutes
                                    
                                    startTimes.Add(currentDate.AddHours(hour).AddMinutes(minutes));
                                }
                                
                                // Assign movies to start times
                                foreach (var startTime in startTimes)
                                {
                                    // Randomly select a movie
                                    var movie = existingMovies[random.Next(existingMovies.Count)];
                                    
                                    // Calculate end time based on movie duration
                                    var endTime = startTime.AddMinutes(movie.DurationMinutes);
                                    
                                    // Set price between $10.99 and $18.99
                                    decimal price = 10.99m + (decimal)random.Next(0, 9);
                                    
                                    // Calculate available seats as total hall capacity
                                    int totalSeats = hall.Rows * hall.SeatsPerRow;

                                    // Create the presentation
                                    presentations.Add(new Presentation
                                    {
                                        Movie = movie,
                                        Hall = hall,
                                        StartTime = startTime,
                                        EndTime = endTime,
                                        Price = price,
                                        HallName = hall.Name,
                                        // Assign a random format from this movie's formats
                                        Format = movie.Formats != null && movie.Formats.Any() 
                                            ? movie.Formats.ElementAt(random.Next(movie.Formats.Count)).Name ?? "Standard"
                                            : "Standard",
                                        // Set available seats to the total capacity of the hall
                                        AvailableSeats = totalSeats
                                    });
                                }
                            }
                        }
                        
                        context.Presentations.AddRange(presentations);
                        await context.SaveChangesAsync();
                    }
                }
            }

            // Initialize SeatPresentation records for all presentations
            await InitializeSeatPresentations(context);
        }
        
        private static async Task UpdatePresentationsAvailableSeats(ApplicationDbContext context)
        {
            // Get presentations with AvailableSeats = 0
            var presentationsToUpdate = await context.Presentations
                .Include(p => p.Hall)
                .Where(p => p.AvailableSeats == 0)
                .ToListAsync();
                
            if (presentationsToUpdate.Any())
            {
                foreach (var presentation in presentationsToUpdate)
                {
                    if (presentation.Hall != null)
                    {
                        // Calculate total seats in the hall
                        int totalSeats = presentation.Hall.Rows * presentation.Hall.SeatsPerRow;
                        
                        // Count booked tickets
                        int bookedSeats = await context.Tickets
                            .CountAsync(t => t.PresentationId == presentation.Id && t.Status != TicketStatus.Cancelled);
                            
                        // Set available seats
                        presentation.AvailableSeats = totalSeats - bookedSeats;
                    }
                }
                
                await context.SaveChangesAsync();
                Console.WriteLine($"Updated AvailableSeats for {presentationsToUpdate.Count} presentations");
            }
        }

        private static async Task InitializeSeatPresentations(ApplicationDbContext context)
        {
            // Get all presentations that don't have any SeatPresentation records
            var presentationsWithoutSeatPresentations = await context.Presentations
                .Include(p => p.Hall)
                .Where(p => !context.SeatPresentations.Any(sp => sp.PresentationId == p.Id))
                .ToListAsync();
            
            if (!presentationsWithoutSeatPresentations.Any())
            {
                return; // No presentations need initialization
            }
            
            Console.WriteLine($"Initializing SeatPresentation records for {presentationsWithoutSeatPresentations.Count} presentations...");
            
            foreach (var presentation in presentationsWithoutSeatPresentations)
            {
                // Get all seats for the presentation's hall
                var seats = await context.Seats
                    .Where(s => s.HallId == presentation.HallId)
                    .ToListAsync();
                
                // Get existing tickets for this presentation
                var bookedSeatIds = await context.Tickets
                    .Where(t => t.PresentationId == presentation.Id && t.Status != TicketStatus.Cancelled)
                    .Select(t => t.SeatId)
                    .ToListAsync();
                
                // Create SeatPresentation records
                var seatPresentations = new List<SeatPresentation>();
                foreach (var seat in seats)
                {
                    seatPresentations.Add(new SeatPresentation
                    {
                        SeatId = seat.Id,
                        PresentationId = presentation.Id,
                        IsAvailable = !bookedSeatIds.Contains(seat.Id), // Only seats without tickets are available
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                
                // Add the SeatPresentation records to the context
                await context.SeatPresentations.AddRangeAsync(seatPresentations);
                
                // Update the presentation's AvailableSeats count
                presentation.AvailableSeats = seats.Count - bookedSeatIds.Count;
                
                Console.WriteLine($"Initialized {seatPresentations.Count} SeatPresentation records for presentation {presentation.Id}");
            }
            
            // Save all changes to the database
            await context.SaveChangesAsync();
        }
    }
}