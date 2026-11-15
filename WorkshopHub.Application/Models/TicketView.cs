using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Models;

// Minimal ticket metadata exposed over HTTP. Storage keys stay server-side.
public sealed record TicketView(TicketId Id, BookingId BookingId, CourseId CourseId, string FileName, string ContentType,
    long Length, DateTimeOffset CreatedAt);

// Everything a ticket needs, read in one query so eligibility is evaluated from a single snapshot.
// ConfirmedAt is the persisted approval time and doubles as the stable ticket issuance time.
public sealed record TicketSource(BookingId BookingId, CourseId CourseId, string? ParticipantName, string CourseTitle,
    int PriceCents, string Status, bool IsDeleted, DateTimeOffset? ConfirmedAt);

// The answer to a ticket request. Only a ready request carries stored metadata; a queued request
// carries the identity that will be used once the worker finishes.
public enum TicketRequestState { Queued, Ready }

public sealed record TicketRequest(TicketId TicketId, TicketRequestState State, TicketView? Ticket)
{
    public bool IsQueued => State == TicketRequestState.Queued;
}
