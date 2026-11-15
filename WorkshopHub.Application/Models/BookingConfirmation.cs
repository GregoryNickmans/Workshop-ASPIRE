namespace WorkshopHub.Application.Models;

// A confirmation always reports the committed booking. TicketId is the stable identity of the ticket,
// whether it is queued or ready; Ticket carries stored metadata only once the row exists. Booking's own
// TicketId is set only when the ticket is persisted, so a queued candidate never looks downloadable.
public sealed record BookingConfirmation(BookingView Booking, TicketView? Ticket, string? TicketWarning,
    TicketRequestState? TicketState = null, Domain.ValueObjects.TicketId? TicketId = null);
