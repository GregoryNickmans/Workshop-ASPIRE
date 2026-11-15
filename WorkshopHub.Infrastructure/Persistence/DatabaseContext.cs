using Microsoft.EntityFrameworkCore;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Infrastructure.Persistence.EntityFramework.EntityConfigurations;

namespace WorkshopHub.Infrastructure.Persistence;

public class DatabaseContext : DbContext
{
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }
    protected DatabaseContext(DbContextOptions options) : base(options) { }
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new BookingConfiguration());
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new TicketConfiguration());
    }
}
