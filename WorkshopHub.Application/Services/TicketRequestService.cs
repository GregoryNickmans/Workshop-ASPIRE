using System.Diagnostics;
using ErrorOr;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Application.Observability;
using WorkshopHub.Contracts.Messaging;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Services;

// Decides whether a booking needs a ticket and, if it does, hands the work to the broker. Nothing here
// renders a PDF or touches storage, so approval never waits for generation.
//
// Known consistency boundary: the booking commit and the broker send are not atomic. A ticket that is
// committed but never submitted is not lost — the identity is derived from the booking, so repeating
// the confirmation or the manual request re-submits the very same work.
public sealed class TicketRequestService(
    ITicketQueryExecutor queryExecutor,
    ITicketRequestPublisher publisher,
    ILogger<TicketRequestService> logger,
    TicketTelemetry telemetry)
{
    public async Task<ErrorOr<TicketRequest>> RequestAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        using var activity = TicketTelemetry.Source.StartActivity("tickets.request", ActivityKind.Internal);
        var started = Stopwatch.GetTimestamp();
        var outcome = "failed";
        try
        {
            // Eligibility comes first so a cancelled booking never receives its old ticket back.
            var source = await queryExecutor.GetSourceAsync(bookingId, cancellationToken);
            var eligibility = CheckEligibility(source);
            if (eligibility.IsError)
            {
                outcome = "rejected";
                return eligibility.Errors;
            }

            // A persisted ticket is authoritative: answer from SQL without downloading the file.
            var existing = await queryExecutor.FindForBookingAsync(bookingId, cancellationToken);
            if (existing is not null)
            {
                outcome = "ready";
                return new TicketRequest(existing.Id, TicketRequestState.Ready, existing);
            }

            var message = Command(source!, TicketId.ForBooking(bookingId));
            try
            {
                var published = await publisher.PublishAsync(message, cancellationToken);
                if (published.IsError) { return published.Errors; }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Ticket command for booking {BookingId} could not be submitted ({Error}).",
                    bookingId.Value, error.GetType().Name);
                return SubmissionFailed();
            }

            outcome = "queued";
            return new TicketRequest(new TicketId(message.TicketId), TicketRequestState.Queued, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        catch (Exception)
        {
            activity?.SetTag("error.type", "unhandled_failure");
            throw;
        }
        finally
        {
            if (outcome == "failed")
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                if (activity?.GetTagItem("error.type") is null) { activity?.SetTag("error.type", "submission_failed"); }
            }
            activity?.SetTag("ticket.outcome", outcome);
            telemetry.Record("request", outcome, Stopwatch.GetElapsedTime(started));
        }
    }

    // The issuance time is the persisted confirmation time, not "now": a retried submission produces the
    // same identity and the same printed timestamp.
    private static CommandMessage Command(TicketSource source, TicketId ticketId) => new(
        TicketMessageContract.Version,
        TicketMessageSubjects.GenerateTicket,
        ticketId.Value,
        source.BookingId.Value,
        source.CourseId.Value,
        source.ConfirmedAt ?? throw new InvalidOperationException("A confirmed booking must have a confirmation time."),
        source.ParticipantName!,
        source.CourseTitle,
        source.PriceCents);

    // Shared by the approval path, the manual endpoint and the worker, so one rule decides eligibility.
    internal static ErrorOr<Success> CheckEligibility(TicketSource? source)
    {
        if (source is null || source.IsDeleted)
        {
            return Error.NotFound("Booking.NotFound", "Booking was not found.");
        }
        if (source.CourseId.Value == Guid.Empty)
        {
            return Error.Conflict("Booking.Legacy", "This legacy booking is not linked to a course.");
        }
        return source.Status switch
        {
            "Pending" => Error.Conflict("Booking.NotConfirmed", "Confirm the booking before issuing a ticket."),
            "Cancelled" => Error.Conflict("Booking.Cancelled", "Cancelled bookings cannot receive a ticket."),
            "Confirmed" when string.IsNullOrWhiteSpace(source.ParticipantName) =>
                Error.Conflict("Booking.Incomplete", "This booking has no attendee name to print."),
            "Confirmed" when source.ConfirmedAt is null =>
                Error.Conflict("Booking.Incomplete", "This booking has no recorded confirmation time."),
            "Confirmed" => Result.Success,
            _ => Error.Conflict("Booking.NotConfirmed", "Only confirmed bookings can receive a ticket.")
        };
    }

    internal static Error SubmissionFailed() =>
        Error.Failure("Ticket.SubmitFailed", "The ticket request could not be queued. Retry ticket generation.");
}
