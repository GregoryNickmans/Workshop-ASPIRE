namespace WorkshopHub.Contracts.Admin.Bookings;

// A successful confirmation, plus whether a ticket came out of it. TicketWarning is safe to display and
// never turns a committed confirmation into an error.
public sealed record AdminBookingConfirmationResponse(AdminBookingResponse Booking, Guid? TicketId,
    string? TicketWarning);
