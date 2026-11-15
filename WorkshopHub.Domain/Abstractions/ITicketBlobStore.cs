using ErrorOr;
using WorkshopHub.Domain.Entities;

namespace WorkshopHub.Domain.Abstractions;

// Blob storage port. Implementations expose nothing but private object bytes.
public interface ITicketBlobStore
{
    Task CreateContainerIfMissingAsync(CancellationToken cancellationToken);

    // Creates a new blob and fails if the key already exists. The generation identity is stored as
    // metadata so a later delivery can prove which render owns the object. Returns the storage ETag.
    Task<ErrorOr<TicketBlobReference>> UploadAsync(string storageKey, ReadOnlyMemory<byte> content,
        TicketBlobIdentity identity, CancellationToken cancellationToken);

    // Returns null when the blob does not exist, so callers can distinguish missing from failure.
    Task<TicketBlob?> TryDownloadAsync(string storageKey, CancellationToken cancellationToken);

    // Metadata-only lookup: length, content type, ETag and generation fingerprint, without the bytes.
    Task<TicketBlobProperties?> TryGetPropertiesAsync(string storageKey, CancellationToken cancellationToken);

    // Deletes only when the stored object still matches the ownership tag from the upload.
    Task DeleteIfUnchangedAsync(string storageKey, string ownershipTag, CancellationToken cancellationToken);
}

public sealed record TicketBlob(Stream Content, long Length);

public sealed record TicketBlobReference(string ETag, long Length);

// Who generated the file stored under a key. Derived from the command, never from the bytes.
public sealed record TicketBlobIdentity(Guid TicketId, Guid BookingId, DateTimeOffset IssuedAt)
{
    // Blob metadata name. Kept lowercase and hyphen-free to stay safe across storage tooling.
    public const string MetadataName = "generation";

    public string Fingerprint => $"{TicketId:D}:{BookingId:D}:{IssuedAt.UtcTicks}";
}

// What storage actually holds. A receiver compares this with the completion event rather than
// trusting the message's own claims.
public sealed record TicketBlobProperties(long Length, string? ContentType, string? ETag, string? Generation)
{
    public bool IsPdf => string.Equals(ContentType, Ticket.PdfContentType, StringComparison.OrdinalIgnoreCase);

    public bool HasValidLength => Length is > 0 and <= Ticket.MaximumLength;

    public bool Matches(TicketBlobIdentity identity) =>
        string.Equals(Generation, identity.Fingerprint, StringComparison.Ordinal);

    // Azure returns a quoted ETag; the comparison tolerates either spelling.
    public bool HasETag(string? expected) =>
        Normalize(ETag) is { Length: > 0 } mine && string.Equals(mine, Normalize(expected), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) => value?.Trim().Trim('"') ?? "";
}
