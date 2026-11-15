using System.Data;
using ErrorOr;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Persistence.EntityFramework.Commands;

// Short SQL-only transaction around the metadata insert. Rendering and blob I/O never run inside it.
public sealed class TicketStore(DatabaseContext database) : ITicketStore
{
    public async Task<ErrorOr<Ticket>> SaveAsync(Ticket candidate, CancellationToken cancellationToken)
    {
        try
        {
            return await database.Database.CreateExecutionStrategy().ExecuteAsync<ErrorOr<Ticket>>(async token =>
            {
                database.ChangeTracker.Clear();
                try
                {
                    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);

                    // Recheck eligibility inside the transaction so a pending or cancelled booking cannot slip through.
                    var eligible = await database.Bookings.AsNoTracking().AnyAsync(x =>
                        x.Id == candidate.BookingId && x.CourseId == candidate.CourseId &&
                        x.Status == "Confirmed" && x.ParticipantName != null, token);
                    if (!eligible)
                    {
                        await transaction.RollbackAsync(token);
                        return Error.Conflict("Booking.NotEligible", "The booking is no longer eligible for a ticket.");
                    }

                    var existing = await database.Tickets.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.BookingId == candidate.BookingId, token);
                    if (existing is not null)
                    {
                        await transaction.RollbackAsync(token);
                        return existing;
                    }

                    database.Tickets.Add(candidate);
                    await database.SaveChangesAsync(token);
                    await transaction.CommitAsync(token);
                    return candidate;
                }
                finally
                {
                    database.ChangeTracker.Clear();
                }
            }, cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Two concurrent candidates passed the read; the unique BookingId index rejected this one.
            database.ChangeTracker.Clear();
            var winner = await database.Tickets.AsNoTracking()
                .SingleOrDefaultAsync(x => x.BookingId == candidate.BookingId, cancellationToken);
            return winner is null
                ? Error.Conflict("Ticket.Duplicate", "This booking already has a ticket. Reload it.")
                : winner;
        }
    }

    // Used to decide whether an ambiguous save actually committed before compensating its blob.
    public Task<bool> ExistsAsync(TicketId ticketId, CancellationToken cancellationToken) =>
        database.Tickets.AsNoTracking().AnyAsync(x => x.Id == ticketId, cancellationToken);
}
