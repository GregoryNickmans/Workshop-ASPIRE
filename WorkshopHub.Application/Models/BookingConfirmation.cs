namespace WorkshopHub.Application.Models;

// A confirmation always reports the committed booking. Ticket is null when no ticket was issued, and
// TicketWarning then explains why without turning a successful confirmation into an error.
public sealed record BookingConfirmation(BookingView Booking, TicketView? Ticket, string? TicketWarning);
