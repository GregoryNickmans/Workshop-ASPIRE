namespace WorkshopHub.Contracts.Admin.Bookings;

public sealed record AdminBookingResponse(Guid Id, Guid CourseId, string Workshop, decimal? Price,
    string? ParticipantName, string? ParticipantEmail, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt, DateTimeOffset? CancelledAt, bool IsLegacy, Guid? TicketId = null);
