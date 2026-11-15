using ErrorOr;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Services;

public sealed class BookingService(
    IQueryExecutor queryExecutor,
    IBookingCommandStore bookingCommandStore,
    TicketRequestService ticketRequestService,
    ILogger<BookingService> logger)
{
    // A committed confirmation is never rolled back by a ticket problem; the warning carries the reason.
    private const string TicketRetryWarning = "Booking confirmed; ticket generation failed. Retry ticket generation.";

    public Task<IReadOnlyList<BookingView>> ListAsync(CancellationToken cancellationToken) => queryExecutor.ListAsync(cancellationToken);

    public async Task<ErrorOr<BookingView>> GetAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        var view = await queryExecutor.GetAsync(bookingId, cancellationToken);
        return view is null ? NotFound() : view;
    }

    public Task<ErrorOr<BookingView>> CreateAsync(CourseId courseId, string? name, string? email, CancellationToken cancellationToken) =>
        bookingCommandStore.CreateBookingAsync(courseId, name, email, cancellationToken);

    public async Task<ErrorOr<BookingConfirmation>> ConfirmAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        // The transition owns its transaction; nothing renders, uploads or publishes inside it.
        var confirmed = await bookingCommandStore.TransitionAsync(bookingId, false, cancellationToken);
        if (confirmed.IsError) { return confirmed.Errors; }

        // Only broker submission runs after the commit, so approval never waits for a PDF.
        return await RequestAfterConfirmationAsync(confirmed.Value, cancellationToken);
    }

    public Task<ErrorOr<BookingView>> CancelAsync(BookingId bookingId, CancellationToken cancellationToken) =>
        bookingCommandStore.TransitionAsync(bookingId, true, cancellationToken);

    private async Task<BookingConfirmation> RequestAfterConfirmationAsync(BookingView booking, CancellationToken cancellationToken)
    {
        ErrorOr<TicketRequest> requested;
        try
        {
            requested = await ticketRequestService.RequestAsync(booking.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up. Confirmation stays committed, so this is not an error to report.
            throw;
        }
        catch (Exception error)
        {
            logger.LogError(error, "Ticket submission after confirming booking {BookingId} failed.", booking.Id.Value);
            return new BookingConfirmation(booking, null, TicketRetryWarning);
        }

        if (requested.IsError)
        {
            return new BookingConfirmation(booking, null, UnavailableReason(requested.Errors));
        }

        // The ticket identity is stable per booking, so the confirmation can report it immediately,
        // whether generation is queued or already persisted.
        // The ticket identity is stable per booking, so the confirmation can report it immediately.
        // Only a persisted ticket is reflected on the booking itself: a queued candidate must not look
        // like an existing ticket anywhere in the response.
        var request = requested.Value;
        return new BookingConfirmation(
            request.State == TicketRequestState.Ready ? booking with { TicketId = request.TicketId } : booking,
            request.Ticket,
            request.IsQueued ? QueuedWarning : null,
            request.State,
            request.TicketId);
    }

    private const string QueuedWarning = "Booking confirmed. Its ticket is being generated; refresh in a moment to download it.";

    private static string UnavailableReason(List<Error> errors) => errors[0].Code switch
    {
        // Legacy and incomplete bookings cannot be fixed by retrying, so the warning says why.
        "Booking.Legacy" => "Booking confirmed, but this legacy booking is not linked to a course, so no ticket can be generated.",
        "Booking.Incomplete" => "Booking confirmed, but this booking has no attendee name to print, so no ticket can be generated.",
        "Booking.Cancelled" => "Booking confirmed, but it was cancelled before a ticket could be generated.",
        _ => TicketRetryWarning
    };

    private static Error NotFound() => Error.NotFound("Booking.NotFound", "Booking was not found.");
}
