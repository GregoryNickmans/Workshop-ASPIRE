using WorkshopHub.Application.Models;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Abstractions;

public interface ICourseQueryExecutor
{
    Task<IReadOnlyList<CourseView>> ListCoursesAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<CourseView?> GetCourseAsync(CourseId courseId, bool includeArchived, CancellationToken cancellationToken);
    Task<BookingPage> ListBookingsAsync(BookingFilter filter, CancellationToken cancellationToken);
}
