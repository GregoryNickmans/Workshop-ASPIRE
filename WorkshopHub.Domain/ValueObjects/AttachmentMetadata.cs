using ErrorOr;

namespace WorkshopHub.Domain.ValueObjects;

public sealed record AttachmentMetadata : ValueObject
{
    public const long MaximumLength = 1024 * 1024;
    private AttachmentMetadata(string storageKey, string fileName, long length)
    {
        StorageKey = storageKey;
        FileName = fileName;
        Length = length;
    }
    public string StorageKey { get; }
    public string FileName { get; }
    public string ContentType => "text/plain";
    public long Length { get; }
    public static ErrorOr<AttachmentMetadata> Create(string storageKey, string? fileName, long length)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 120 ||
            fileName.Contains('/') || fileName.Contains('\\') || fileName.Any(char.IsControl) ||
            !fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(storageKey))
        {
            return Error.Validation("Booking.AttachmentFileName", "Use a .txt filename up to 120 characters without path separators.");
        }
        if (length is <= 0 or > MaximumLength)
        {
            return Error.Validation("Booking.AttachmentLength", "Choose a synthetic text attachment between 1 byte and 1 MiB.");
        }
        return new AttachmentMetadata(storageKey, fileName, length);
    }
}
