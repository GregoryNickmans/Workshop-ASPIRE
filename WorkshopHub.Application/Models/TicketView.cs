using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Models;

// Minimal ticket metadata exposed over HTTP. Storage keys stay server-side.
public sealed record TicketView(TicketId Id, BookingId BookingId, CourseId CourseId, string FileName, string ContentType,
    long Length, DateTimeOffset CreatedAt);

// Everything a ticket needs, read in one query so eligibility is evaluated from a single snapshot.
public sealed record TicketSource(BookingId BookingId, CourseId CourseId, string? ParticipantName, string CourseTitle,
    int PriceCents, string Status, bool IsDeleted);

// Stored metadata plus the PDF bytes, so callers need no second storage round-trip.
// Metadata always comes from the persisted row, so repeated requests return identical values.
// WasCreated is false when an earlier or concurrent request had already issued this ticket.
public sealed record IssuedTicket(TicketView Ticket, byte[] Pdf, bool WasCreated);
