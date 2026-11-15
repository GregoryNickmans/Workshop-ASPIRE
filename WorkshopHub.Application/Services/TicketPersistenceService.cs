using System.Diagnostics;
using ErrorOr;
using WorkshopHub.Application.Observability;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Contracts.Messaging;
using WorkshopHub.Domain.Abstractions;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Services;

// The API's answer to one event delivery.
public enum TicketPersistenceOutcome
{
    // The persisted row exists (or was already there) with matching metadata.
    Saved,

    // A different candidate owns the booking; this one is resolved and its file was cleaned up.
    Superseded,

    // The booking is no longer eligible, so nothing may be persisted and the candidate is cleaned up.
    Rejected,

    // Broker, SQL or storage may recover on retry: leave the event unacknowledged.
    Transient,

    // The event is unsupported or contradicts storage: retrying it cannot help.
    Terminal
}

public sealed record TicketPersistenceResult(TicketPersistenceOutcome Outcome, TicketView? Ticket);

// Turns a completion event into stored metadata. The blob is the source of truth: every claim in the
// event is checked against what storage actually holds before a row is written.
public sealed class TicketPersistenceService(
    ITicketBlobStore blobStore,
    ITicketStore ticketStore,
    ITicketQueryExecutor queryExecutor,
    ILogger<TicketPersistenceService> logger,
    TicketTelemetry telemetry)
{
    public async Task<TicketPersistenceResult> HandleAsync(EventMessage message, CancellationToken cancellationToken)
    {
        using var activity = TicketTelemetry.Source.StartActivity("tickets.persist", ActivityKind.Internal);
        var started = Stopwatch.GetTimestamp();
        var outcome = "failed";
        try
        {
            var result = await HandleCoreAsync(message, cancellationToken);
            outcome = result.Outcome switch
            {
                TicketPersistenceOutcome.Saved => "saved",
                TicketPersistenceOutcome.Superseded => "superseded",
                TicketPersistenceOutcome.Rejected => "rejected",
                TicketPersistenceOutcome.Transient => "transient",
                TicketPersistenceOutcome.Terminal => "terminal",
                _ => "failed"
            };
            if (outcome is "transient" or "terminal" or "failed")
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.type", outcome == "terminal" ? "unsupported_completion" : "persistence_failed");
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        catch (Exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", "unhandled_failure");
            throw;
        }
        finally
        {
            activity?.SetTag("ticket.outcome", outcome);
            telemetry.Record("persist", outcome, Stopwatch.GetElapsedTime(started));
        }
    }

    private async Task<TicketPersistenceResult> HandleCoreAsync(EventMessage message, CancellationToken cancellationToken)
    {
        if (!IsSupported(message))
        {
            return new(TicketPersistenceOutcome.Terminal, null);
        }

        var ticketId = new TicketId(message.TicketId);
        var bookingId = new BookingId(message.BookingId);
        var courseId = new CourseId(message.CourseId);
        var identity = new TicketBlobIdentity(message.TicketId, message.BookingId, message.IssuedAt);
        var storageKey = Ticket.StorageKeyFor(courseId, ticketId);

        var stored = await blobStore.TryGetPropertiesAsync(storageKey, cancellationToken);
        if (stored is null)
        {
            // The worker publishes only after uploading, so a missing blob is unusual; retry rather than
            // risk committing a row for a file that is not there.
            logger.LogWarning("Completion event for ticket {TicketId} has no stored file yet.", message.TicketId);
            return new(TicketPersistenceOutcome.Transient, null);
        }
        if (!stored.Matches(identity))
        {
            logger.LogWarning("Completion event for ticket {TicketId} does not match the stored generation.", message.TicketId);
            return new(TicketPersistenceOutcome.Terminal, null);
        }
        if (!stored.IsPdf || !stored.HasValidLength || message.Length != stored.Length || !stored.HasETag(message.ETag))
        {
            logger.LogWarning("Completion event for ticket {TicketId} reports metadata that storage does not confirm.",
                message.TicketId);
            return new(TicketPersistenceOutcome.Terminal, null);
        }

        var committed = await queryExecutor.FindForBookingAsync(bookingId, cancellationToken);
        if (committed is not null)
        {
            return await ResolveExistingAsync(committed, ticketId, storageKey, message, cancellationToken);
        }

        var created = Ticket.Create(bookingId, courseId, ticketId, stored.Length, message.IssuedAt);
        if (created.IsError) { return new(TicketPersistenceOutcome.Terminal, null); }

        return await SaveAsync(created.Value, storageKey, cancellationToken);
    }

    private async Task<TicketPersistenceResult> ResolveExistingAsync(TicketView committed, TicketId ticketId,
        string storageKey, EventMessage message, CancellationToken cancellationToken)
    {
        if (committed.Id == ticketId)
        {
            // Redelivery of an event whose row already landed: idempotent only when the metadata agrees.
            var matches = committed.Length == message.Length && committed.CreatedAt == message.IssuedAt;
            return matches
                ? new(TicketPersistenceOutcome.Saved, committed)
                : new(TicketPersistenceOutcome.Terminal, null);
        }

        // A different candidate won. This event's file is safe to remove only after proving no committed
        // ticket (cancelled bookings included) refers to it.
        return await CleanupAsync(ticketId, storageKey, message.ETag, cancellationToken)
            ? new(TicketPersistenceOutcome.Superseded, committed)
            : new(TicketPersistenceOutcome.Transient, null);
    }

    private async Task<TicketPersistenceResult> SaveAsync(Ticket candidate, string storageKey,
        CancellationToken cancellationToken)
    {
        ErrorOr<Ticket> saved;
        try
        {
            // The store owns a short serializable transaction and rechecks eligibility inside it.
            saved = await ticketStore.SaveAsync(candidate, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Ambiguous commit: never delete until a fresh read can say whether this row landed.
            logger.LogWarning("Ticket metadata save for booking {BookingId} failed ambiguously ({Error}); reconciling.",
                candidate.BookingId.Value, error.GetType().Name);
            return await ReconcileAsync(candidate, cancellationToken);
        }

        if (saved.IsError)
        {
            // Ineligible booking or a rejected candidate: nothing may reference this file.
            logger.LogInformation("Ticket {TicketId} was not persisted ({Code}); releasing its stored file.",
                candidate.Id.Value, saved.Errors[0].Code);
            return await CleanupAsync(candidate.Id, storageKey, null, cancellationToken)
                ? new(TicketPersistenceOutcome.Rejected, null)
                : new(TicketPersistenceOutcome.Transient, null);
        }

        if (saved.Value.Id != candidate.Id)
        {
            // Another candidate won the unique booking index; this file is an orphan.
            return await CleanupAsync(candidate.Id, storageKey, null, cancellationToken)
                ? new(TicketPersistenceOutcome.Superseded, Map(saved.Value))
                : new(TicketPersistenceOutcome.Transient, null);
        }

        return new(TicketPersistenceOutcome.Saved, Map(saved.Value));
    }

    private async Task<TicketPersistenceResult> ReconcileAsync(Ticket candidate, CancellationToken cancellationToken)
    {
        try
        {
            if (!await ticketStore.ExistsAsync(candidate.Id, CancellationToken.None))
            {
                return new(TicketPersistenceOutcome.Transient, null);
            }
        }
        catch (Exception error)
        {
            logger.LogWarning("Could not reconcile ticket {TicketId} ({Error}); keeping its stored file.",
                candidate.Id.Value, error.GetType().Name);
            return new(TicketPersistenceOutcome.Transient, null);
        }

        return new(TicketPersistenceOutcome.Saved, Map(candidate));
    }

    // Removes a losing or rejected candidate's file, but only when no committed ticket refers to it.
    private async Task<bool> CleanupAsync(TicketId ticketId, string storageKey, string? ownershipTag,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await ticketStore.ExistsAsync(ticketId, cancellationToken))
            {
                // A committed ticket owns this file; deleting it would break a downloadable ticket.
                return true;
            }

            var tag = ownershipTag;
            if (string.IsNullOrWhiteSpace(tag))
            {
                // Re-read the tag so the delete can only ever remove the file this event described.
                tag = (await blobStore.TryGetPropertiesAsync(storageKey, cancellationToken))?.ETag;
            }
            if (string.IsNullOrWhiteSpace(tag)) { return true; }

            await blobStore.DeleteIfUnchangedAsync(storageKey, tag, cancellationToken);
            return true;
        }
        catch (Exception error)
        {
            // Unknown state: keep the blob and let the event be retried.
            logger.LogWarning("Failed to clean up the candidate file for ticket {TicketId} ({Error}).",
                ticketId.Value, error.GetType().Name);
            return false;
        }
    }

    private static bool IsSupported(EventMessage message) =>
        message.Version == TicketMessageContract.Version &&
        message.Subject == TicketMessageSubjects.TicketGenerated &&
        message.TicketId != Guid.Empty && message.BookingId != Guid.Empty && message.CourseId != Guid.Empty &&
        message.IssuedAt != default && !string.IsNullOrWhiteSpace(message.ETag);

    private static TicketView Map(Ticket ticket) => new(ticket.Id, ticket.BookingId, ticket.CourseId,
        ticket.FileName, ticket.ContentType, ticket.Length, ticket.CreatedAt);
}
