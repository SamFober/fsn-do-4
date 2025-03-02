using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using WebApi.Models;
using WebApi.Models.Responses;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Hall> Halls { get; set; } = null!;
    public DbSet<Seat> Seats { get; set; } = null!;
    public DbSet<Movie> Movies { get; set; } = null!;
    public DbSet<Presentation> Presentations { get; set; } = null!;
    public DbSet<Ticket> Tickets { get; set; } = null!;
    public DbSet<TicketOrder> TicketOrders { get; set; } = null!;
    public DbSet<TicketOrderItem> TicketOrderItems { get; set; } = null!;
    public DbSet<SeatLock> SeatLocks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Seat>()
            .HasIndex(s => new { s.HallId, s.RowNumber, s.SeatNumber })
            .IsUnique();

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.PresentationId, t.SeatId })
            .IsUnique();

        modelBuilder.Entity<SeatLock>()
            .HasOne(sl => sl.Seat)
            .WithMany()
            .HasForeignKey(sl => sl.SeatId);

        modelBuilder.Entity<TicketOrder>()
            .Property(e => e.AvailableOptions)
            .HasColumnType("json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<Dictionary<string, SeatingOption>>(
                    v, new JsonSerializerOptions()) ?? new Dictionary<string, SeatingOption>()
            )
            .Metadata.SetValueComparer(
                new ValueComparer<Dictionary<string, SeatingOption>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c != null ? c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())) : 0,
                    c => c != null ? new Dictionary<string, SeatingOption>(c) : new Dictionary<string, SeatingOption>()
                )
            );
    }
} 