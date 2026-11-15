namespace WorkshopHub.Contracts.Tickets;

// Ticket metadata only. The stored file name and private storage location stay server-side.
public sealed record TicketResponse(Guid Id, Guid BookingId, Guid CourseId, string FileName, string ContentType,
    long Length, DateTimeOffset CreatedAt);
