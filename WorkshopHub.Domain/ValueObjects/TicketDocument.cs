namespace WorkshopHub.Domain.ValueObjects;

// Renderer input: already-validated domain values. No caller HTML, URLs or raw booking entities.
public sealed record TicketDocument(
    string CourseTitle,
    string AttendeeName,
    int PriceCents,
    Guid BookingId,
    Guid TicketId,
    DateTimeOffset IssuedAt);
