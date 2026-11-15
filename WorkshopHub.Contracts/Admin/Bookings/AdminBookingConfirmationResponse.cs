namespace WorkshopHub.Contracts.Admin.Bookings;

// A successful confirmation, plus whether a ticket came out of it. TicketWarning is safe to display and
// never turns a committed confirmation into an error. TicketStatus is "Ready" when the ticket row exists
// and "Queued" when generation is still in flight; TicketId names the ticket in both cases.
public sealed record AdminBookingConfirmationResponse(AdminBookingResponse Booking, Guid? TicketId,
    string? TicketStatus, string? TicketWarning);
