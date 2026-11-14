using ErrorOr;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Services;

public sealed class CourseService(ICourseQueryExecutor courseQueryExecutor, IBookingCommandStore bookingCommandStore)
{
    public Task<IReadOnlyList<CourseView>> ListAsync(bool includeArchived, CancellationToken cancellationToken) =>
        courseQueryExecutor.ListCoursesAsync(includeArchived, cancellationToken);

    public async Task<ErrorOr<CourseView>> GetAsync(CourseId courseId, bool includeArchived, CancellationToken cancellationToken)
    {
        var course = await courseQueryExecutor.GetCourseAsync(courseId, includeArchived, cancellationToken);
        return course is null ? Error.NotFound("Course.NotFound", "Course was not found.") : course;
    }

    public Task<ErrorOr<CourseView>> CreateAsync(string? title, string? description, decimal price, int capacity, CancellationToken cancellationToken)
    {
        var course = Course.Create(title, description, price, capacity);
        return course.IsError ? Task.FromResult<ErrorOr<CourseView>>(course.Errors) : bookingCommandStore.CreateCourseAsync(course.Value, cancellationToken);
    }

    public Task<ErrorOr<CourseView>> UpdateAsync(CourseId courseId, string? title, string? description, decimal price,
        int capacity, int revision, CancellationToken cancellationToken) =>
        bookingCommandStore.UpdateCourseAsync(courseId, title, description, price, capacity, revision, cancellationToken);

    public Task<ErrorOr<CourseView>> ArchiveAsync(CourseId courseId, int revision, CancellationToken cancellationToken) =>
        bookingCommandStore.ArchiveCourseAsync(courseId, revision, cancellationToken);

    public Task<ErrorOr<BookingPage>> ListBookingsAsync(CourseId? courseId, string? status, string? search, int page,
        int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
        {
            return Task.FromResult<ErrorOr<BookingPage>>(Error.Validation("Booking.Pagination", "Page must be positive and page size must be 1–100."));
        }
        if (!string.IsNullOrEmpty(status) && status is not ("Pending" or "Confirmed" or "Cancelled"))
        {
            return Task.FromResult<ErrorOr<BookingPage>>(Error.Validation("Booking.Status", "Choose Pending, Confirmed or Cancelled."));
        }
        if (search?.Length > 200)
        {
            return Task.FromResult<ErrorOr<BookingPage>>(Error.Validation("Booking.Search", "Search must be at most 200 characters."));
        }
        return ListValidatedAsync(new BookingFilter(courseId, status, search?.Trim(), page, pageSize), cancellationToken);
    }

    private async Task<ErrorOr<BookingPage>> ListValidatedAsync(BookingFilter filter, CancellationToken cancellationToken) =>
        await courseQueryExecutor.ListBookingsAsync(filter, cancellationToken);
}
