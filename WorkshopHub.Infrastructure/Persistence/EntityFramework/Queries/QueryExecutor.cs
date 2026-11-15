using Microsoft.EntityFrameworkCore;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Persistence.EntityFramework.Queries;

public sealed class QueryExecutor(DatabaseContext database) : IQueryExecutor, ICourseQueryExecutor
{
    // Historical internal cloud consumer; public API no longer exposes this bounded list.
    public async Task<IReadOnlyList<BookingView>> ListAsync(CancellationToken cancellationToken) =>
        (await database.Bookings.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(25)
            .ToListAsync(cancellationToken)).Select(BookingProjection.Map).ToArray();
    public async Task<BookingView?> GetAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        var booking = await database.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bookingId, cancellationToken);
        return booking is null ? null : BookingProjection.Map(booking);
    }
    public async Task<IReadOnlyList<CourseView>> ListCoursesAsync(bool includeArchived, CancellationToken cancellationToken) =>
        (await database.Courses.AsNoTracking().Where(x => includeArchived || !x.IsArchived)
            .OrderBy(x => x.Title).ThenBy(x => x.Id).ToListAsync(cancellationToken)).Select(BookingProjection.Map).ToArray();
    public async Task<CourseView?> GetCourseAsync(CourseId courseId, bool includeArchived, CancellationToken cancellationToken)
    {
        var course = await database.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId && (includeArchived || !x.IsArchived), cancellationToken);
        return course is null ? null : BookingProjection.Map(course);
    }
    public Task<BookingPage> ListBookingsAsync(BookingFilter filter, CancellationToken cancellationToken) =>
        database.Database.CreateExecutionStrategy().ExecuteAsync<BookingPage>(async token =>
        {
            // Retry the count and page together within one read transaction.
            await using var transaction = await database.Database.BeginTransactionAsync(token);
            var query = database.Bookings.AsNoTracking();
            if (!string.IsNullOrEmpty(filter.Search))
            {
                var search = filter.Search.ToLowerInvariant();
                if (Guid.TryParse(search, out var bookingId))
                {
                    query = query.Where(x => x.Id == new BookingId(bookingId));
                }
                else if (search.Length is >= 8 and <= 32 && search.All(Uri.IsHexDigit))
                {
                    // The UI displays the first eight hex digits; search those without loading all IDs into memory.
                    query = database.Bookings.FromSqlInterpolated($"SELECT * FROM [Bookings] WHERE LOWER(REPLACE(CAST([Id] AS varchar(36)), '-', '')) LIKE {search + "%"}")
                        .AsNoTracking();
                }
                else
                {
                    query = query.Where(x => (x.ParticipantName != null && x.ParticipantName.ToLower().Contains(search)) ||
                        (x.ParticipantEmail != null && x.ParticipantEmail.Contains(search)));
                }
            }
            if (filter.CourseId is not null)
            { query = query.Where(x => x.CourseId == filter.CourseId); }
            if (!string.IsNullOrEmpty(filter.Status))
            { query = query.Where(x => x.Status == filter.Status); }
            var count = await query.CountAsync(token);
            var bookings = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(token);
            await transaction.CommitAsync(token);
            return new BookingPage(bookings.Select(BookingProjection.Map).ToArray(), count, filter.Page, filter.PageSize);
        }, cancellationToken);
}
