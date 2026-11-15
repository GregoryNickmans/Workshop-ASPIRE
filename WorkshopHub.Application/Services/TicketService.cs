using ErrorOr;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Abstractions;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Services;

// Read side of tickets: metadata and bytes. Generation lives in the worker and persistence in the
// completion-event handler, so this service never renders, uploads or writes.
public sealed class TicketService(
    ITicketQueryExecutor queryExecutor,
    ITicketBlobStore blobStore,
    ILogger<TicketService> logger)
{
    public async Task<ErrorOr<TicketView>> GetAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        var ticket = await queryExecutor.GetAsync(ticketId, cancellationToken);
        return ticket is null ? Error.NotFound("Ticket.NotFound", "Ticket was not found.") : ticket;
    }

    // Rechecks eligibility on every download so a cancelled booking stops issuing bytes.
    public async Task<ErrorOr<TicketBlob>> OpenAsync(TicketView ticket, CancellationToken cancellationToken)
    {
        var source = await queryExecutor.GetSourceAsync(ticket.BookingId, cancellationToken);
        var eligibility = TicketRequestService.CheckEligibility(source);
        if (eligibility.IsError) { return eligibility.Errors; }

        var blob = await blobStore.TryDownloadAsync(Ticket.StorageKeyFor(ticket.CourseId, ticket.Id), cancellationToken);
        if (blob is null)
        {
            return Error.NotFound("Ticket.ContentMissing", "The stored ticket file is no longer available.");
        }

        logger.LogDebug("Streaming ticket {TicketId} from private storage.", ticket.Id.Value);
        return blob;
    }

    public Task CreateContainerIfMissingAsync(CancellationToken cancellationToken) =>
        blobStore.CreateContainerIfMissingAsync(cancellationToken);
}
