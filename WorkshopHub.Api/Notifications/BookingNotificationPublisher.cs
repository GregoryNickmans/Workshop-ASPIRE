using Microsoft.Azure.SignalR.Management;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Contracts.Bookings;

namespace WorkshopHub.Api.Notifications;

// One reusable Serverless management context. No ASP.NET Core Hub or Blazor circuit is involved.
public sealed class BookingNotificationPublisher(IConfiguration configuration, ILoggerFactory loggerFactory)
    : IBookingNotifications, IAsyncDisposable
{
    private readonly SemaphoreSlim _initialization = new(1, 1);
    private ServiceManager? _manager;
    private ServiceHubContext? _context;

    private async Task<ServiceHubContext> GetContextAsync(CancellationToken cancellationToken)
    {
        await _initialization.WaitAsync(cancellationToken);
        try
        {
            if (_context is null)
            {
                _manager ??= new ServiceManagerBuilder().WithOptions(options =>
                {
                    options.ConnectionString = configuration.GetConnectionString("notifications")
                        ?? throw new InvalidOperationException("Aspire connection 'notifications' is required.");
                    options.ServiceTransportType = ServiceTransportType.Transient;
                }).WithLoggerFactory(loggerFactory).BuildServiceManager();
                _context = await _manager.CreateHubContextAsync("bookings", cancellationToken);
            }
            return _context;
        }
        finally { _initialization.Release(); }
    }

    public async Task PublishAsync(Guid bookingId, CancellationToken cancellationToken) =>
        await (await GetContextAsync(cancellationToken)).Clients.All
            .SendCoreAsync("BookingChanged", [bookingId.ToString("D")], cancellationToken);

    public async Task<NotificationConnection> NegotiateAsync(CancellationToken cancellationToken)
    {
        // Receive-only anonymous local demonstration. Do not trust browser-supplied user IDs.
        var result = await (await GetContextAsync(cancellationToken)).NegotiateAsync(new NegotiationOptions
        {
            EnableDetailedErrors = false,
            TokenLifetime = TimeSpan.FromMinutes(10)
        }, cancellationToken);
        return new(result.Url ?? throw new InvalidOperationException("Missing negotiation URL."),
            result.AccessToken ?? throw new InvalidOperationException("Missing negotiation token."));
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null) { await _context.DisposeAsync(); }
        _manager?.Dispose();
        _initialization.Dispose();
    }
}
