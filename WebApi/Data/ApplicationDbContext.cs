using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
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
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<TicketOrderItem> TicketOrderItems { get; set; } = null!;
    public DbSet<SeatLock> SeatLocks { get; set; } = null!;
    public DbSet<MovieFormat> MovieFormats { get; set; } = null!;
    public DbSet<SeatPresentation> SeatPresentations { get; set; } = null!;
    public DbSet<ConcessionItem> ConcessionItems { get; set; } = null!;
    public DbSet<OrderConcessionItem> OrderConcessionItems { get; set; } = null!;
    public DbSet<Review> Reviews { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Seat entity
        modelBuilder.Entity<Seat>()
            .HasIndex(s => new { s.HallId, s.RowNumber, s.SeatNumber })
            .IsUnique();

        modelBuilder.Entity<Seat>()
            .HasOne(s => s.Hall)
            .WithMany(h => h.Seats) // Ensure this is set to the collection in Hall
            .HasForeignKey(s => s.HallId);

        // Configure SeatPresentation entity
        modelBuilder.Entity<SeatPresentation>()
            .HasIndex(sp => new { sp.SeatId, sp.PresentationId })
            .IsUnique();

        modelBuilder.Entity<SeatPresentation>()
            .HasOne(sp => sp.Seat)
            .WithMany()
            .HasForeignKey(sp => sp.SeatId);

        modelBuilder.Entity<SeatPresentation>()
            .HasOne(sp => sp.Presentation)
            .WithMany()
            .HasForeignKey(sp => sp.PresentationId);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.PresentationId, t.SeatId })
            .IsUnique();

        modelBuilder.Entity<SeatLock>()
            .HasOne(sl => sl.Seat)
            .WithMany()
            .HasForeignKey(sl => sl.SeatId);

        // Configure MovieFormat entity
        modelBuilder.Entity<MovieFormat>()
            .HasOne(mf => mf.Movie)
            .WithMany(m => m.Formats)
            .HasForeignKey(mf => mf.MovieId);

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

        modelBuilder.Entity<OrderConcessionItem>()
            .HasOne(oci => oci.Order)
            .WithMany(o => o.ConcessionItems)
            .HasForeignKey(oci => oci.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderConcessionItem>()
            .HasOne(oci => oci.ConcessionItem)
            .WithMany()
            .HasForeignKey(oci => oci.ConcessionItemId)
            .OnDelete(DeleteBehavior.Restrict);
        // Configure Review entity
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Movie)
            .WithMany(m => m.Reviews)  // One movie has many reviews
            .HasForeignKey(r => r.MovieId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}