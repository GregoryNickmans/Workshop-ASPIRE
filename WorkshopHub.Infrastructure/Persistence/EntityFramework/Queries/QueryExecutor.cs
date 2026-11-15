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
            .ToListAsync(cancellationToken)).Select(x => BookingProjection.Map(x)).ToArray();

    // Detail reads carry ticket availability so the portal never has to issue a follow-up request.
    public async Task<BookingView?> GetAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        var booking = await database.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bookingId, cancellationToken);
        if (booking is null) { return null; }
        var ticketId = await database.Tickets.AsNoTracking()
            .Where(x => x.BookingId == bookingId).Select(x => x.Id).SingleOrDefaultAsync(cancellationToken);
        return BookingProjection.Map(booking, ticketId.Value == Guid.Empty ? null : ticketId);
    }

    public async Task<IReadOnlyList<CourseView>> ListCoursesAsync(bool includeArchived, CancellationToken cancellationToken) =>
        (await database.Courses.AsNoTracking().Where(x => includeArchived || !x.IsArchived)
            .OrderBy(x => x.Title).ThenBy(x => x.Id).ToListAsync(cancellationToken)).Select(x => BookingProjection.Map(x)).ToArray();
    public async Task<CourseView?> GetCourseAsync(CourseId courseId, bool includeArchived, CancellationToken cancellationToken)
    {
        var course = await database.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId && (includeArchived || !x.IsArchived), cancellationToken);
        return course is null ? null : BookingProjection.Map(course);
    }
    public Task<BookingPage> ListBookingsAsync(BookingFilter filter, CancellationToken cancellationToken) =>
        database.Database.CreateExecutionStrategy().ExecuteAsync<BookingPage>(async token =>
        {
            // Retry the count, page and ticket metadata together within one read transaction.
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
            // One extra query for the whole page instead of a lookup per row.
            var bookingIds = bookings.Select(x => x.Id).ToArray();
            var tickets = bookingIds.Length == 0
                ? []
                : await database.Tickets.AsNoTracking().Where(x => bookingIds.Contains(x.BookingId))
                    .Select(x => new { x.BookingId, x.Id }).ToListAsync(token);
            await transaction.CommitAsync(token);
            var ticketByBooking = tickets.ToDictionary(x => x.BookingId, x => x.Id);
            return new BookingPage(bookings.Select(x =>
                BookingProjection.Map(x, ticketByBooking.TryGetValue(x.Id, out var ticketId) ? ticketId : null)).ToArray(),
                count, filter.Page, filter.PageSize);
        }, cancellationToken);
}
