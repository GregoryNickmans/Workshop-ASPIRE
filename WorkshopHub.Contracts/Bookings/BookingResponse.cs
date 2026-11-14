namespace WorkshopHub.Contracts.Bookings;

public sealed record BookingResponse(Guid Id, Guid CourseId, string Workshop, decimal? Price,
    string? ParticipantName, string? ParticipantEmail, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt, DateTimeOffset? CancelledAt, bool IsLegacy);
