using ErrorOr;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Abstractions;

public interface ITicketQueryExecutor
{
    Task<TicketView?> GetAsync(TicketId ticketId, CancellationToken cancellationToken);
    Task<TicketView?> FindForBookingAsync(BookingId bookingId, CancellationToken cancellationToken);
    Task<TicketSource?> GetSourceAsync(BookingId bookingId, CancellationToken cancellationToken);
}

// The ticket store owns its own short SQL transaction. Rendering and blob work happen outside it.
public interface ITicketStore
{
    // Returns the existing ticket when one is already persisted, otherwise the newly inserted ticket.
    // Returns errors when the booking is missing, legacy, pending or cancelled.
    Task<ErrorOr<Ticket>> SaveAsync(Ticket candidate, CancellationToken cancellationToken);

    // Resolves whether a metadata row for this candidate was committed, for use after an ambiguous save.
    Task<bool> ExistsAsync(TicketId ticketId, CancellationToken cancellationToken);
}
