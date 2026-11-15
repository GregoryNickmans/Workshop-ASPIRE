namespace WorkshopHub.Contracts.Messaging;

// Version of both directions. A receiver rejects anything it does not understand.
public static class TicketMessageContract
{
    public const int Version = 1;
}

// Everything the PDF worker needs, and nothing it must not have: no PDF bytes, no storage key, no
// storage credentials. The render input and the storage path are derived from these identifiers.
// IssuedAt is the booking's persisted confirmation time, so a retried submission renders the same ticket.
public sealed record CommandMessage(
    int Version,
    string Subject,
    Guid TicketId,
    Guid BookingId,
    Guid CourseId,
    DateTimeOffset IssuedAt,
    string AttendeeName,
    string CourseTitle,
    int PriceCents);
