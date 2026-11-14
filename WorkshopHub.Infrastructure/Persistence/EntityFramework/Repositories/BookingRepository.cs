using ErrorOr;
using Microsoft.EntityFrameworkCore;
using System.Data;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class BookingRepository(DatabaseContext database) : IBookingRepository
{
    public Task<Booking?> FindAsync(BookingId bookingId, CancellationToken cancellationToken) =>
        database.Bookings.SingleOrDefaultAsync(x => x.Id == bookingId, cancellationToken);
    public Task AddAsync(Booking booking, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        database.Bookings.Add(booking);
        return Task.CompletedTask;
    }
    public async Task SaveAsync(CancellationToken cancellationToken) => await database.SaveChangesAsync(cancellationToken);
    public async Task<IReadOnlyList<Booking>> ListActiveAsync(CancellationToken cancellationToken) => await database.Bookings.ToListAsync(cancellationToken);
    public async Task<ErrorOr<SeedResult>> ExecuteDatasetChangeAsync(Func<CancellationToken, Task<ErrorOr<SeedResult>>> change, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await change(cancellationToken);
            if (result.IsError)
            {
                await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
                return result.Errors;
            }
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            database.ChangeTracker.Clear();
            throw;
        }
    }
}
