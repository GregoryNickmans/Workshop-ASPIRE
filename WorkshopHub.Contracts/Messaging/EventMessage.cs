namespace WorkshopHub.Contracts.Messaging;

// Completion report from the PDF worker. The receiver re-reads the blob and compares these values with
// what is actually stored, so a stale or forged message cannot commit metadata for a different file.
public sealed record EventMessage(
    int Version,
    string Subject,
    Guid TicketId,
    Guid BookingId,
    Guid CourseId,
    DateTimeOffset IssuedAt,
    long Length,
    string ETag);
