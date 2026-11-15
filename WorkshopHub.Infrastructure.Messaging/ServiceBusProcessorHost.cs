using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkshopHub.Infrastructure.Messaging;

// Owns the processor's lifetime: starts listening after the host is up and stops before disposal.
// The processor is a singleton so a restart (an AppHost restart of this resource) reuses one instance.
internal abstract class ServiceBusProcessorHost<TProcessor>(
    TProcessor processor,
    ILogger logger) : IHostedService where TProcessor : TicketMessageProcessorBase
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await processor.StartAsync(cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A missing or refused broker must not take the whole host down; the processor host fails
            // visibly in the dashboard while HTTP endpoints stay usable.
            logger.LogError("Service Bus processor could not start ({Error}). The host continues without it.",
                error.GetType().Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => processor.StopAsync(cancellationToken);
}

internal sealed class TicketGenerationHost(TicketGenerationProcessor processor, ILogger<TicketGenerationHost> logger)
    : ServiceBusProcessorHost<TicketGenerationProcessor>(processor, logger);

internal sealed class TicketPersistenceHost(TicketPersistenceProcessor processor, ILogger<TicketPersistenceHost> logger)
    : ServiceBusProcessorHost<TicketPersistenceProcessor>(processor, logger);
