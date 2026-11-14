using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Domain.Events;

public sealed record BookingConfirmed(BookingId BookingId, DateTimeOffset ConfirmedAt);
