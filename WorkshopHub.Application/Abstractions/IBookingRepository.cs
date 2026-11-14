using ErrorOr;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Abstractions;

public interface IBookingRepository
{
    Task<Booking?> FindAsync(BookingId bookingId, CancellationToken cancellationToken);
    Task AddAsync(Booking booking, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Booking>> ListActiveAsync(CancellationToken cancellationToken);
    Task<ErrorOr<SeedResult>> ExecuteDatasetChangeAsync(
        Func<CancellationToken, Task<ErrorOr<SeedResult>>> change, CancellationToken cancellationToken);
}
