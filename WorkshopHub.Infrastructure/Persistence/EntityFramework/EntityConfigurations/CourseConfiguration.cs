using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkshopHub.Domain;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Persistence.EntityFramework.EntityConfigurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> course)
    {
        course.ToTable("Courses", table =>
        {
            table.HasCheckConstraint("CK_Courses_Seats", "[MaximumParticipants] > 0 AND [OccupiedSeats] >= 0 AND [OccupiedSeats] <= [MaximumParticipants]");
            table.HasCheckConstraint("CK_Courses_Price", "[PriceCents] >= 0");
        });
        course.HasKey(x => x.Id);
        course.Property(x => x.Id).HasConversion(id => id.Value, value => new CourseId(value)).ValueGeneratedNever();
        course.Property(x => x.Title).HasConversion(x => x.Value, x => WorkshopTitle.Create(x).Value).HasMaxLength(80).IsRequired();
        course.Property(x => x.ShortDescription).HasMaxLength(300).IsRequired();
        course.Property(x => x.PriceCents).IsRequired();
        course.Property(x => x.MaximumParticipants).IsRequired();
        course.Property(x => x.OccupiedSeats).HasDefaultValue(0).IsRequired();
        course.Property(x => x.IsArchived).HasDefaultValue(false).IsRequired();
        course.Property(x => x.Revision).HasDefaultValue(0).IsConcurrencyToken().IsRequired();
        course.Ignore(x => x.Price);
        course.Ignore(x => x.RemainingSeats);
    }
}
