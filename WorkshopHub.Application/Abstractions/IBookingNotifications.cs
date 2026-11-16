namespace WorkshopHub.Application.Abstractions;

// An invalidation hint, never authoritative booking data or a promise of durable delivery.
public interface IBookingNotifications
{
    Task PublishAsync(Guid bookingId, CancellationToken cancellationToken);
}
