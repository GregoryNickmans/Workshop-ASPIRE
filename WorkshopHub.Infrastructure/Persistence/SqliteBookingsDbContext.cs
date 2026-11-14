using Microsoft.EntityFrameworkCore;
using WorkshopHub.Domain.Entities;

namespace WorkshopHub.Infrastructure.Persistence;

public sealed class SqliteBookingsDbContext(DbContextOptions<SqliteBookingsDbContext> options) : DatabaseContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var booking = modelBuilder.Entity<Booking>();
        // UTC ticks preserve ordering, precision and nullable values on SQLite (no DateTimeOffset ordering support).
        booking.Property(x => x.CreatedAt).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        booking.Property(x => x.ConfirmedAt).HasConversion(x => x.HasValue ? x.Value.UtcTicks : (long?)null,
            x => x.HasValue ? new DateTimeOffset(x.Value, TimeSpan.Zero) : null);
        booking.Property(x => x.CancelledAt).HasConversion(x => x.HasValue ? x.Value.UtcTicks : (long?)null,
            x => x.HasValue ? new DateTimeOffset(x.Value, TimeSpan.Zero) : null);
        booking.Property(x => x.NotificationPublishedAt).HasConversion(x => x.HasValue ? x.Value.UtcTicks : (long?)null,
            x => x.HasValue ? new DateTimeOffset(x.Value, TimeSpan.Zero) : null);
    }
}
