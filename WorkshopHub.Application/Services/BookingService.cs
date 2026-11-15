using ErrorOr;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Services;

public sealed class BookingService(
    IQueryExecutor queryExecutor,
    IBookingCommandStore bookingCommandStore,
    TicketService ticketService,
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
        // The transition owns its transaction; nothing renders or uploads inside it.
        var confirmed = await bookingCommandStore.TransitionAsync(bookingId, false, cancellationToken);
        if (confirmed.IsError) { return confirmed.Errors; }

        // Issuance runs strictly after the commit and on its own short transactions.
        return await IssueAfterConfirmationAsync(confirmed.Value, cancellationToken);
    }

    public Task<ErrorOr<BookingView>> CancelAsync(BookingId bookingId, CancellationToken cancellationToken) =>
        bookingCommandStore.TransitionAsync(bookingId, true, cancellationToken);

    private async Task<BookingConfirmation> IssueAfterConfirmationAsync(BookingView booking, CancellationToken cancellationToken)
    {
        ErrorOr<IssuedTicket> issued;
        try
        {
            issued = await ticketService.IssueAsync(booking.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up. Confirmation stays committed, so this is not an error to report.
            throw;
        }
        catch (Exception error)
        {
            logger.LogError(error, "Ticket issuance after confirming booking {BookingId} failed.", booking.Id.Value);
            return new BookingConfirmation(booking, null, TicketRetryWarning);
        }

        return issued.IsError
            ? new BookingConfirmation(booking, null, UnavailableReason(issued.Errors))
            // The ticket ID is persisted, so the returned booking reflects it without a second read.
            : new BookingConfirmation(booking with { TicketId = issued.Value.Ticket.Id }, issued.Value.Ticket, null);
    }

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
