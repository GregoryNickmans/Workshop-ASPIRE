using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkshopHub.Domain;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Persistence.EntityFramework.EntityConfigurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> booking)
    {
        booking.ToTable("Bookings", table =>
        {
            table.HasCheckConstraint("CK_Bookings_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')");
            table.HasCheckConstraint("CK_Bookings_Contact", "[CourseId] IS NULL OR ([ParticipantName] IS NOT NULL AND [ParticipantEmail] IS NOT NULL AND [PriceCents] IS NOT NULL)");
        });
        booking.HasKey(x => x.Id);
        booking.Property(x => x.Id).HasConversion(id => id.Value, value => new BookingId(value)).ValueGeneratedNever();
        booking.Property(x => x.Workshop).HasConversion(title => title.Value, value => WorkshopTitle.Create(value).Value).HasMaxLength(80).IsRequired();
        booking.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending").IsRequired();
        booking.Property(x => x.CreatedAt).IsRequired();
        booking.Property(x => x.ConfirmedAt);
        booking.Property(x => x.CancelledAt);
        booking.Property(x => x.NotificationPublishedAt);
        booking.Property(x => x.CourseId).HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null,
            value => value.HasValue ? new CourseId(value.Value) : null);
        booking.Property(x => x.PriceCents);
        booking.Property(x => x.ParticipantName).HasMaxLength(100);
        booking.Property(x => x.ParticipantEmail).HasMaxLength(254);
        booking.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        booking.Property(x => x.AttachmentBlobName).HasMaxLength(100);
        booking.Property(x => x.AttachmentFileName).HasMaxLength(120);
        booking.Property(x => x.AttachmentContentType).HasMaxLength(100);
        booking.Property(x => x.AttachmentLength);
        booking.Property(x => x.IsDeleted).HasDefaultValue(false).IsRequired();
        booking.HasQueryFilter(x => !x.IsDeleted);
        booking.Ignore(x => x.DomainEvents);
        booking.HasIndex(x => x.CreatedAt);
        booking.HasIndex(x => new { x.CourseId, x.ParticipantEmail }).IsUnique()
            .HasFilter("[CourseId] IS NOT NULL AND [IsDeleted] = 0 AND [Status] IN ('Pending', 'Confirmed')");
    }
}
