using Microsoft.EntityFrameworkCore;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;
using WorkshopHub.Infrastructure.Persistence;

namespace WorkshopHub.Seeder;

public sealed class SeedDataset(DatabaseContext database)
{
    public Task<SeedResponse> ApplyAsync(CancellationToken token) =>
        database.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            database.ChangeTracker.Clear();
            await database.Database.EnsureDeletedAsync(token);
            await database.Database.MigrateAsync(token);

            // Original catalog extracted from the AddCourseBooking migration.
            AddCourse("b5274a15-4cac-4fd8-9c5c-ff5252a6cf01", "Creative thinking, practical tools",
                "Build a fresh perspective with hands-on exercises in ideation, collaboration and creative problem solving. Sample course.",
                7900, 12);
            AddCourse("b5274a15-4cac-4fd8-9c5c-ff5252a6cf02", "Design for the everyday",
                "Explore the principles behind thoughtful digital experiences, from first sketch to an accessible prototype. Sample course.",
                12500, 8);
            AddCourse("b5274a15-4cac-4fd8-9c5c-ff5252a6cf03", "Your first web application",
                "Turn an idea into a working web app. A friendly introduction to the building blocks of modern development. Sample course.",
                14900, 10);
            AddCourse("b5274a15-4cac-4fd8-9c5c-ff5252a6cf04", "Present with confidence",
                "Find your voice and shape a story that connects. Practice clear, confident communication in a supportive workshop. Sample course.",
                6500, 16);

            var courseCount = database.ChangeTracker.Entries<Course>().Count();
            await database.SaveChangesAsync(token);
            return new SeedResponse(courseCount, 0);
        });

    private void AddCourse(string id, string title, string description, int priceCents, int capacity)
    {
        var result = Course.Create(title, description, priceCents / 100m, capacity);
        if (result.IsError) { throw new InvalidOperationException("Invalid original course dataset."); }
        var course = result.Value;
        database.Entry(course).Property(x => x.Id).CurrentValue = new CourseId(Guid.Parse(id));
        database.Courses.Add(course);
    }
}

public sealed record SeedResponse(int Courses, int Bookings);
