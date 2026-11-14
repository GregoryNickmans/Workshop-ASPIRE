using ErrorOr;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Services;

public sealed class BookingService(IQueryExecutor queryExecutor, IBookingCommandStore bookingCommandStore)
{
    public Task<IReadOnlyList<BookingView>> ListAsync(CancellationToken cancellationToken) => queryExecutor.ListAsync(cancellationToken);

    public async Task<ErrorOr<BookingView>> GetAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        var view = await queryExecutor.GetAsync(bookingId, cancellationToken);
        return view is null ? NotFound() : view;
    }

    public Task<ErrorOr<BookingView>> CreateAsync(CourseId courseId, string? name, string? email, CancellationToken cancellationToken) =>
        bookingCommandStore.CreateBookingAsync(courseId, name, email, cancellationToken);

    public Task<ErrorOr<BookingView>> ConfirmAsync(BookingId bookingId, CancellationToken cancellationToken) =>
        bookingCommandStore.TransitionAsync(bookingId, false, cancellationToken);

    public Task<ErrorOr<BookingView>> CancelAsync(BookingId bookingId, CancellationToken cancellationToken) =>
        bookingCommandStore.TransitionAsync(bookingId, true, cancellationToken);

    private static Error NotFound() => Error.NotFound("Booking.NotFound", "Booking was not found.");
}
