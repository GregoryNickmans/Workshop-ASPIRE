using System.Diagnostics;
using ErrorOr;
using WorkshopHub.Application.Observability;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Contracts.Messaging;
using WorkshopHub.Domain.Abstractions;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Services;

// The worker's answer to one delivery, so settlement never has to guess from an error message.
public enum TicketGenerationOutcome
{
    // The blob is stored and the completion event was published: complete the command.
    Completed,

    // Rendering, storage or the broker failed in a way that may succeed next time: abandon and retry.
    Transient,

    // The command is unsupported or conflicts with an existing generation: nothing can fix it.
    Terminal
}

public sealed record TicketGenerationResult(TicketGenerationOutcome Outcome, ErrorOr<EventMessage> Completion);

// PDF worker side of generation: render, upload create-only, then report completion. No SQL here.
// Every step is safe to repeat, because the ticket identity and issuance time come from the command.
public sealed class TicketGenerationService(
    ITicketRenderer renderer,
    ITicketBlobStore blobStore,
    ITicketCompletionPublisher publisher,
    ILogger<TicketGenerationService> logger,
    TicketTelemetry telemetry)
{
    public async Task<TicketGenerationResult> GenerateAsync(CommandMessage command, CancellationToken cancellationToken)
    {
        using var activity = TicketTelemetry.Source.StartActivity("tickets.generate", ActivityKind.Internal);
        var started = Stopwatch.GetTimestamp();
        var outcome = "failed";
        try
        {
            var result = await GenerateCoreAsync(command, activity, cancellationToken);
            outcome = result.Outcome switch
            {
                TicketGenerationOutcome.Completed => "completed",
                TicketGenerationOutcome.Transient => "transient",
                TicketGenerationOutcome.Terminal => "terminal",
                _ => "failed"
            };
            if (outcome != "completed")
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.type", outcome == "terminal" ? "unsupported_generation" : "generation_failed");
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
            telemetry.Record("generate", outcome, Stopwatch.GetElapsedTime(started));
        }
    }

    private async Task<TicketGenerationResult> GenerateCoreAsync(CommandMessage command, Activity? activity,
        CancellationToken cancellationToken)
    {
        var plan = Plan(command);
        if (plan.IsError) { return new(TicketGenerationOutcome.Terminal, plan.Errors); }
        var (identity, storageKey) = plan.Value;

        // A previous delivery may already have uploaded the file; its real length and ETag are the truth,
        // never the length of a new render.
        var stored = await blobStore.TryGetPropertiesAsync(storageKey, cancellationToken);
        TicketBlobReference? reference;
        if (stored is not null)
        {
            activity?.SetTag("ticket.generation.path", "reused");
            if (!stored.Matches(identity))
            {
                // The key is occupied by a different generation. Overwriting or adopting it could corrupt
                // a real ticket, so this delivery is a conflict rather than a retry.
                return new(TicketGenerationOutcome.Terminal,
                    Error.Conflict("Ticket.IdentityConflict", "Storage holds a file for a different ticket generation."));
            }
            reference = Recovered(storageKey, stored);
        }
        else
        {
            activity?.SetTag("ticket.generation.path", "rendered");
            reference = await RenderAndUploadAsync(command, identity, storageKey, cancellationToken);
        }

        if (reference is null)
        {
            return new(TicketGenerationOutcome.Transient,
                Error.Failure("Ticket.StorageFailed", "The ticket file could not be confirmed in storage."));
        }

        var completion = Completion(command, reference);
        try
        {
            // The stored blob is kept even when this fails, so a retry only has to republish the event.
            var published = await publisher.PublishAsync(completion, cancellationToken);
            if (published.IsError) { return new(TicketGenerationOutcome.Transient, published.Errors); }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Completion event for ticket {TicketId} could not be published ({Error}).",
                command.TicketId, error.GetType().Name);
            return new(TicketGenerationOutcome.Transient,
                Error.Failure("Ticket.EventPublishFailed", "The ticket was stored but its completion event was not published."));
        }

        return new(TicketGenerationOutcome.Completed, completion);
    }

    private async Task<TicketBlobReference?> RenderAndUploadAsync(CommandMessage command, TicketBlobIdentity identity,
        string storageKey, CancellationToken cancellationToken)
    {
        var document = new TicketDocument(command.CourseTitle, command.AttendeeName, command.PriceCents,
            command.BookingId, command.TicketId, command.IssuedAt);
        var rendered = await renderer.RenderAsync(document, cancellationToken);
        if (rendered.IsError)
        {
            // Missing Chromium and timeouts are both worth retrying; nothing was stored.
            return null;
        }

        var uploaded = await blobStore.UploadAsync(storageKey, rendered.Value, identity, cancellationToken);
        if (!uploaded.IsError)
        {
            logger.LogInformation("Ticket PDF for ticket {TicketId} was generated and stored.", command.TicketId);
            return uploaded.Value;
        }

        // Duplicate delivery or an ambiguous acknowledgement: the stored object decides, not this render.
        var current = await blobStore.TryGetPropertiesAsync(storageKey, cancellationToken);
        return current is not null && current.Matches(identity) ? Recovered(storageKey, current) : null;
    }

    // Trusts only a blob that proves it belongs to this generation and looks like a PDF.
    private TicketBlobReference? Recovered(string storageKey, TicketBlobProperties stored)
    {
        if (stored.IsPdf && stored.HasValidLength && stored.HasETag(stored.ETag))
        {
            return new TicketBlobReference(stored.ETag!, stored.Length);
        }
        logger.LogWarning("Stored ticket file {StorageKey} does not look like a valid ticket and cannot be reported.", storageKey);
        return null;
    }

    private static ErrorOr<(TicketBlobIdentity Identity, string StorageKey)> Plan(CommandMessage command)
    {
        if (command.Version != TicketMessageContract.Version)
        {
            return Error.Validation("Ticket.Version", "Unsupported message version.");
        }
        if (command.Subject != TicketMessageSubjects.GenerateTicket)
        {
            return Error.Validation("Ticket.Subject", "Unsupported message subject.");
        }
        if (command.TicketId == Guid.Empty || command.BookingId == Guid.Empty || command.CourseId == Guid.Empty)
        {
            return Error.Validation("Ticket.Identity", "The ticket, booking and course references are required.");
        }
        if (command.IssuedAt == default)
        {
            return Error.Validation("Ticket.IssuedAt", "A ticket requires its original issuance time.");
        }
        if (string.IsNullOrWhiteSpace(command.AttendeeName) || command.AttendeeName.Length > 100)
        {
            return Error.Validation("Ticket.Attendee", "A ticket requires an attendee name.");
        }
        if (string.IsNullOrWhiteSpace(command.CourseTitle) || command.CourseTitle.Length > 200)
        {
            return Error.Validation("Ticket.Course", "A ticket requires a course title.");
        }

        // The key is derived from the identifiers, never supplied by the message.
        var storageKey = Ticket.StorageKeyFor(new CourseId(command.CourseId), new TicketId(command.TicketId));
        return (new TicketBlobIdentity(command.TicketId, command.BookingId, command.IssuedAt), storageKey);
    }

    private static EventMessage Completion(CommandMessage command, TicketBlobReference reference) => new(
        TicketMessageContract.Version,
        TicketMessageSubjects.TicketGenerated,
        command.TicketId,
        command.BookingId,
        command.CourseId,
        command.IssuedAt,
        reference.Length,
        reference.ETag);
}
