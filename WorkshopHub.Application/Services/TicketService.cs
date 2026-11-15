using ErrorOr;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Abstractions;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Services;

// Orchestrates ticket creation and delivery independently of HTTP so a future worker can reuse it.
// SQL sits in short transactions; rendering and blob work never run inside one.
public sealed class TicketService(
    ITicketQueryExecutor queryExecutor,
    ITicketStore ticketStore,
    ITicketRenderer renderer,
    ITicketBlobStore blobStore,
    TimeProvider timeProvider,
    ILogger<TicketService> logger)
{
    public async Task<ErrorOr<TicketView>> GetAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        var ticket = await queryExecutor.GetAsync(ticketId, cancellationToken);
        return ticket is null ? Error.NotFound("Ticket.NotFound", "Ticket was not found.") : ticket;
    }

    // Idempotent: a repeated request returns the ticket persisted by the first one.
    public async Task<ErrorOr<IssuedTicket>> IssueAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        // Eligibility is checked before the existing-ticket fast path, so a cancelled booking never
        // receives its old ticket back.
        var source = await queryExecutor.GetSourceAsync(bookingId, cancellationToken);
        var eligibility = CheckEligibility(source);
        if (eligibility.IsError) { return eligibility.Errors; }

        var existing = await queryExecutor.FindForBookingAsync(bookingId, cancellationToken);
        if (existing is not null) { return await LoadExistingAsync(existing, cancellationToken); }

        var ticketId = new TicketId(Guid.NewGuid());
        var document = new TicketDocument(source!.CourseTitle, source.ParticipantName!, source.PriceCents,
            bookingId.Value, ticketId.Value, timeProvider.GetUtcNow());
        var rendered = await renderer.RenderAsync(document, cancellationToken);
        if (rendered.IsError) { return rendered.Errors; }

        var created = Ticket.Create(bookingId, source.CourseId, ticketId, rendered.Value.LongLength, timeProvider);
        if (created.IsError) { return created.Errors; }
        var candidate = created.Value;

        var uploaded = await blobStore.UploadAsync(candidate.StorageKey, rendered.Value, cancellationToken);
        if (uploaded.IsError) { return uploaded.Errors; }

        var saved = await PersistAsync(candidate, uploaded.Value, cancellationToken);
        if (saved.IsError) { return saved.Errors; }

        // A concurrent request may have won the unique BookingId index; return that persisted ticket.
        return saved.Value.Id == candidate.Id
            ? new IssuedTicket(Map(candidate), rendered.Value, WasCreated: true)
            : await LoadExistingAsync(Map(saved.Value), cancellationToken);
    }

    // Rechecks eligibility on every download so a cancelled booking stops issuing bytes.
    public async Task<ErrorOr<TicketBlob>> OpenAsync(TicketView ticket, CancellationToken cancellationToken)
    {
        var source = await queryExecutor.GetSourceAsync(ticket.BookingId, cancellationToken);
        var eligibility = CheckEligibility(source);
        if (eligibility.IsError) { return eligibility.Errors; }

        var blob = await blobStore.TryDownloadAsync(Ticket.StorageKeyFor(ticket.CourseId, ticket.Id), cancellationToken);
        return blob is null
            ? Error.NotFound("Ticket.ContentMissing", "The stored ticket file is no longer available.")
            : blob;
    }

    public Task CreateContainerIfMissingAsync(CancellationToken cancellationToken) =>
        blobStore.CreateContainerIfMissingAsync(cancellationToken);

    private async Task<ErrorOr<IssuedTicket>> LoadExistingAsync(TicketView ticket, CancellationToken cancellationToken)
    {
        var blob = await blobStore.TryDownloadAsync(Ticket.StorageKeyFor(ticket.CourseId, ticket.Id), cancellationToken);
        if (blob is null)
        {
            return Error.NotFound("Ticket.ContentMissing", "The stored ticket file is no longer available.");
        }

        await using var content = blob.Content;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        // Metadata comes from the stored row, never from a rebuilt entity, so repeat calls are stable.
        return new IssuedTicket(ticket, buffer.ToArray(), WasCreated: false);
    }

    // Inserts metadata and compensates the candidate blob when this request did not win.
    private async Task<ErrorOr<Ticket>> PersistAsync(Ticket candidate, string ownershipTag, CancellationToken cancellationToken)
    {
        ErrorOr<Ticket> saved;
        try
        {
            saved = await ticketStore.SaveAsync(candidate, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Ambiguous commit: a fresh read decides whether the metadata actually landed.
            logger.LogWarning("Ticket metadata save for booking {BookingId} failed ambiguously ({Error}); verifying committed state.",
                candidate.BookingId.Value, error.GetType().Name);
            return await ReconcileAsync(candidate, ownershipTag);
        }

        if (saved.IsError || saved.Value.Id != candidate.Id)
        {
            // Rejected, or another candidate won the unique booking index: this PDF is an orphan.
            await DeleteCandidateAsync(candidate.StorageKey, ownershipTag);
        }
        return saved;
    }

    private async Task<ErrorOr<Ticket>> ReconcileAsync(Ticket candidate, string ownershipTag)
    {
        try
        {
            if (!await ticketStore.ExistsAsync(candidate.Id, CancellationToken.None))
            {
                await DeleteCandidateAsync(candidate.StorageKey, ownershipTag);
            }
        }
        catch (Exception error)
        {
            // Unknown state: keep the blob rather than risk deleting a valid ticket's file.
            logger.LogWarning("Could not reconcile ticket {TicketId} after a failed save ({Error}); leaving its stored file in place.",
                candidate.Id.Value, error.GetType().Name);
        }
        return Error.Failure("Ticket.SaveFailed", "The ticket could not be saved. Retry the request.");
    }

    private async Task DeleteCandidateAsync(string storageKey, string ownershipTag)
    {
        try
        {
            await blobStore.DeleteIfUnchangedAsync(storageKey, ownershipTag, CancellationToken.None);
        }
        catch (Exception error)
        {
            logger.LogWarning("Candidate ticket file for storage key {StorageKey} could not be cleaned up ({Error}).",
                storageKey, error.GetType().Name);
        }
    }

    private static ErrorOr<Success> CheckEligibility(TicketSource? source)
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
            "Confirmed" => Result.Success,
            _ => Error.Conflict("Booking.NotConfirmed", "Only confirmed bookings can receive a ticket.")
        };
    }

    internal static TicketView Map(Ticket ticket) => new(ticket.Id, ticket.BookingId, ticket.CourseId,
        ticket.FileName, ticket.ContentType, ticket.Length, ticket.CreatedAt);
}
