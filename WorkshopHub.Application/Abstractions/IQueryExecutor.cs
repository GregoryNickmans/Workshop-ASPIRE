using WorkshopHub.Application.Models;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Abstractions;

public interface IQueryExecutor
{
    Task<IReadOnlyList<BookingView>> ListAsync(CancellationToken cancellationToken);
    Task<BookingView?> GetAsync(BookingId bookingId, CancellationToken cancellationToken);
}
