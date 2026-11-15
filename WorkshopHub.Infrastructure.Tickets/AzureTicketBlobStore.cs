using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErrorOr;
using Microsoft.Extensions.Logging;
using WorkshopHub.Domain.Abstractions;
using WorkshopHub.Domain.Entities;

namespace WorkshopHub.Infrastructure.Tickets;

// The container client comes from the Aspire "Tickets" resource reference; the physical container
// name is lowercase and set only in the AppHost. Access is always private: no SAS URLs, no
// credential-bearing URLs and no anonymous public access.
public sealed class AzureTicketBlobStore(BlobContainerClient container, ILogger<AzureTicketBlobStore> logger)
    : ITicketBlobStore
{
    public async Task CreateContainerIfMissingAsync(CancellationToken cancellationToken)
    {
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        logger.LogInformation("Ticket storage container '{Container}' is ready.", container.Name);
    }

    public async Task<ErrorOr<TicketBlobReference>> UploadAsync(string storageKey, ReadOnlyMemory<byte> content,
        TicketBlobIdentity identity, CancellationToken cancellationToken)
    {
        try
        {
            // IfNoneMatch = * means "fail if the key exists": a candidate can never overwrite a winner.
            // The generation fingerprint is stored with the object so any later delivery can prove it owns it.
            var response = await container.GetBlobClient(storageKey).UploadAsync(
                BinaryData.FromBytes(content.ToArray()),
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = Ticket.PdfContentType },
                    Metadata = new Dictionary<string, string> { [TicketBlobIdentity.MetadataName] = identity.Fingerprint },
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                }, cancellationToken);
            return new TicketBlobReference(response.Value.ETag.ToString(), content.Length);
        }
        catch (RequestFailedException error) when (error.Status is 409 or 412)
        {
            return Error.Conflict("Ticket.StorageConflict", "A ticket file with this reference already exists.");
        }
        catch (RequestFailedException error)
        {
            logger.LogError(error, "Uploading the ticket PDF for storage key {StorageKey} failed.", storageKey);
            return Error.Failure("Ticket.StorageFailed", "The ticket file could not be stored.");
        }
    }

    public async Task<TicketBlob?> TryDownloadAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.GetBlobClient(storageKey).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new TicketBlob(response.Value.Content, response.Value.Details.ContentLength);
        }
        catch (RequestFailedException error) when (error.Status == 404)
        {
            return null;
        }
    }

    public async Task<TicketBlobProperties?> TryGetPropertiesAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.GetBlobClient(storageKey).GetPropertiesAsync(cancellationToken: cancellationToken);
            var properties = response.Value;
            properties.Metadata.TryGetValue(TicketBlobIdentity.MetadataName, out var generation);
            return new TicketBlobProperties(properties.ContentLength, properties.ContentType, properties.ETag.ToString(), generation);
        }
        catch (RequestFailedException error) when (error.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteIfUnchangedAsync(string storageKey, string ownershipTag, CancellationToken cancellationToken)
    {
        // Delete only the blob this caller uploaded; an ETag mismatch means a different candidate owns the key.
        // The tag saved at upload time is always a quoted Azure ETag.
        var conditions = string.IsNullOrWhiteSpace(ownershipTag) ? null : new BlobRequestConditions { IfMatch = new ETag(ownershipTag) };
        await container.GetBlobClient(storageKey)
            .DeleteIfExistsAsync(DeleteSnapshotsOption.None, conditions, cancellationToken);
    }
}
