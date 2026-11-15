using ErrorOr;

namespace WorkshopHub.Domain.Abstractions;

// Blob storage port. Implementations expose nothing but private object bytes.
public interface ITicketBlobStore
{
    Task CreateContainerIfMissingAsync(CancellationToken cancellationToken);

    // Creates a new blob and fails if the key already exists. Returns the storage ETag so the
    // caller can later delete only the object it uploaded.
    Task<ErrorOr<string>> UploadAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    // Returns null when the blob does not exist, so callers can distinguish missing from failure.
    Task<TicketBlob?> TryDownloadAsync(string storageKey, CancellationToken cancellationToken);

    // Deletes only when the stored object still matches the ownership tag from the upload.
    Task DeleteIfUnchangedAsync(string storageKey, string ownershipTag, CancellationToken cancellationToken);
}

public sealed record TicketBlob(Stream Content, long Length);
