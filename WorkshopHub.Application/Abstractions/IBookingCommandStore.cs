using ErrorOr;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Abstractions;

// The command store owns the complete inventory transaction, including persistence and retries.
public interface IBookingCommandStore
{
    Task<ErrorOr<BookingView>> CreateBookingAsync(CourseId courseId, string? name, string? email, CancellationToken cancellationToken);
    Task<ErrorOr<BookingView>> TransitionAsync(BookingId bookingId, bool cancel, CancellationToken cancellationToken);
    Task<ErrorOr<CourseView>> CreateCourseAsync(Course course, CancellationToken cancellationToken);
    Task<ErrorOr<CourseView>> UpdateCourseAsync(CourseId courseId, string? title, string? description, decimal price,
        int capacity, int revision, CancellationToken cancellationToken);
    Task<ErrorOr<CourseView>> ArchiveCourseAsync(CourseId courseId, int revision, CancellationToken cancellationToken);
}
