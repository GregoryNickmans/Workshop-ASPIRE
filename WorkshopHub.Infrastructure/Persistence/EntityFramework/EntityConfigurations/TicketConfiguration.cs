using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Persistence.EntityFramework.EntityConfigurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> ticket)
    {
        ticket.ToTable("Tickets", table =>
        {
            table.HasCheckConstraint("CK_Tickets_Length", "[Length] > 0 AND [Length] <= 10485760");
            table.HasCheckConstraint("CK_Tickets_ContentType", "[ContentType] = 'application/pdf'");
            table.HasCheckConstraint("CK_Tickets_StorageKey", "[StorageKey] LIKE '%.pdf'");
        });
        ticket.HasKey(x => x.Id);
        ticket.Property(x => x.Id).HasConversion(id => id.Value, value => new TicketId(value)).ValueGeneratedNever();
        ticket.Property(x => x.BookingId).HasConversion(id => id.Value, value => new BookingId(value)).IsRequired();
        ticket.Property(x => x.CourseId).HasConversion(id => id.Value, value => new CourseId(value)).IsRequired();
        ticket.Property(x => x.StorageKey).HasMaxLength(200).IsRequired();
        ticket.Property(x => x.FileName).HasMaxLength(120).IsRequired();
        ticket.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
        ticket.Property(x => x.Length).IsRequired();
        ticket.Property(x => x.CreatedAt).IsRequired();
        ticket.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        // One ticket per booking: the database decides the winner between concurrent candidates.
        ticket.HasIndex(x => x.BookingId).IsUnique();
        ticket.HasIndex(x => x.CourseId);
        ticket.HasIndex(x => x.CreatedAt);
    }
}
