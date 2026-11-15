using Microsoft.EntityFrameworkCore;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Persistence.EntityFramework.Queries;

public sealed class TicketQueryExecutor(DatabaseContext database) : ITicketQueryExecutor
{
    public async Task<TicketView?> GetAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        var ticket = await database.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        return ticket is null ? null : Map(ticket);
    }

    public async Task<TicketView?> FindForBookingAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        var ticket = await database.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);
        return ticket is null ? null : Map(ticket);
    }

    // Ignores the booking soft-delete filter so a deleted booking returns NotFound instead of incomplete data.
    // ConfirmedAt is projected because it is the stable issuance time used for asynchronous generation.
    public async Task<TicketSource?> GetSourceAsync(BookingId bookingId, CancellationToken cancellationToken) =>
        await database.Bookings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == bookingId)
            .Select(x => new TicketSource(x.Id, x.CourseId ?? default, x.ParticipantName, x.Workshop.Value,
                x.PriceCents ?? 0, x.Status, x.IsDeleted, x.ConfirmedAt))
            .SingleOrDefaultAsync(cancellationToken);

    private static TicketView Map(Ticket ticket) => new(ticket.Id, ticket.BookingId, ticket.CourseId,
        ticket.FileName, ticket.ContentType, ticket.Length, ticket.CreatedAt);
}
