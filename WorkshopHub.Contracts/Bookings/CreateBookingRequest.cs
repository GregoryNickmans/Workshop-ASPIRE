namespace WorkshopHub.Contracts.Bookings;

public sealed record CreateBookingRequest(Guid CourseId, string ParticipantName, string ParticipantEmail);
