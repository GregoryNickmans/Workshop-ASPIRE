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
        // Migration-owned sample catalog: inserted once, never reset on restart or after archive/edit.
        course.HasData(
            new
            {
                Id = new CourseId(Guid.Parse("b5274a15-4cac-4fd8-9c5c-ff5252a6cf01")),
                Title = WorkshopTitle.Create("Creative thinking, practical tools").Value,
                ShortDescription = "Build a fresh perspective with hands-on exercises in ideation, collaboration and creative problem solving. Sample course.",
                PriceCents = 7900,
                MaximumParticipants = 12,
                OccupiedSeats = 0,
                IsArchived = false,
                Revision = 0
            },
            new
            {
                Id = new CourseId(Guid.Parse("b5274a15-4cac-4fd8-9c5c-ff5252a6cf02")),
                Title = WorkshopTitle.Create("Design for the everyday").Value,
                ShortDescription = "Explore the principles behind thoughtful digital experiences, from first sketch to an accessible prototype. Sample course.",
                PriceCents = 12500,
                MaximumParticipants = 8,
                OccupiedSeats = 0,
                IsArchived = false,
                Revision = 0
            },
            new
            {
                Id = new CourseId(Guid.Parse("b5274a15-4cac-4fd8-9c5c-ff5252a6cf03")),
                Title = WorkshopTitle.Create("Your first web application").Value,
                ShortDescription = "Turn an idea into a working web app. A friendly introduction to the building blocks of modern development. Sample course.",
                PriceCents = 14900,
                MaximumParticipants = 10,
                OccupiedSeats = 0,
                IsArchived = false,
                Revision = 0
            },
            new
            {
                Id = new CourseId(Guid.Parse("b5274a15-4cac-4fd8-9c5c-ff5252a6cf04")),
                Title = WorkshopTitle.Create("Present with confidence").Value,
                ShortDescription = "Find your voice and shape a story that connects. Practice clear, confident communication in a supportive workshop. Sample course.",
                PriceCents = 6500,
                MaximumParticipants = 16,
                OccupiedSeats = 0,
                IsArchived = false,
                Revision = 0
            });
    }
}
