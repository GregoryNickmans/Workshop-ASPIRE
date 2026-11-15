using WorkshopHub.Contracts.Admin.Bookings;
using WorkshopHub.Web.Endpoints;

namespace WorkshopHub.Web.Components.Pages;

// The outcome of one confirmation or explicit ticket generation, kept apart from the filtered list.
// A notice must not disappear just because the record no longer matches the active filter.
public sealed class BookingTicketNotice(Guid bookingId, Guid? ticketId, string message, bool canRetryTicket)
{
    public Guid BookingId { get; } = bookingId;
    public Guid? TicketId { get; } = ticketId;
    public string Message { get; } = message;
    // Only true when regenerating could succeed: a legacy or incomplete booking needs data repair, not a retry.
    public bool CanRetryTicket { get; } = canRetryTicket;

    public string? DownloadUrl => TicketId is { } id ? TicketDownloads.Url(id) : null;

    // Retry is offered for anything that is not a data problem, including an uncertain transport outcome.
    public static bool IsRetryable(AdminBookingResponse booking) =>
        booking.Status == "Confirmed" && !booking.IsLegacy && !string.IsNullOrWhiteSpace(booking.ParticipantName);
}
