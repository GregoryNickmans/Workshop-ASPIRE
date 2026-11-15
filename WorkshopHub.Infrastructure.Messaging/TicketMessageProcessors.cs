using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Services;
using WorkshopHub.Contracts.Messaging;

namespace WorkshopHub.Infrastructure.Messaging;

// The answer to one delivery. Only Complete and DeadLetter settle a message; Abandon hands it back to
// the SDK so redelivery and the subscription's max-delivery dead-letter policy apply.
public enum TicketSettlement { Complete, Abandon, DeadLetter }

// Shared loop for both processors. A scope is created per delivery so no EF context, store or handler is
// ever shared between messages, and failures are logged without message bodies or attendee data.
public abstract class TicketMessageProcessorBase : IAsyncDisposable
{
    private ServiceBusProcessor? _processor;

    protected TicketMessageProcessorBase(ServiceBusClient client, ServiceBusProcessorOptions options,
        string subscription, ILogger logger)
    {
        Client = client;
        Options = options;
        Subscription = subscription;
        Logger = logger;
    }

    protected ServiceBusClient Client { get; }
    protected ServiceBusProcessorOptions Options { get; }
    protected string Subscription { get; }
    protected ILogger Logger { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _processor = Client.CreateProcessor(ServiceBusTopology.Topic, Subscription, Options);
        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;
        await _processor.StartProcessingAsync(cancellationToken);
        Logger.LogInformation("Listening on {Topic}/{Subscription}.", ServiceBusTopology.Topic, Subscription);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is null) { return; }
        await _processor.StopProcessingAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is null) { return; }
        _processor.ProcessMessageAsync -= OnMessageAsync;
        _processor.ProcessErrorAsync -= OnErrorAsync;
        await _processor.DisposeAsync();
    }

    protected abstract Task<TicketSettlement> HandleAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken);

    // Reads the body and rejects anything that is not the expected envelope, so a malformed or foreign
    // message becomes a dead letter instead of an endless retry.
    protected static bool TryRead<T>(ServiceBusReceivedMessage message, Func<T, bool> validate, out T? result) where T : class
    {
        result = null;
        if (message.Body is null || message.Body.ToMemory().Length == 0) { return false; }
        try
        {
            result = TicketMessages.Deserialize<T>(message.Body.ToString());
            return result is not null && validate(result);
        }
        catch (Exception error) when (error is JsonException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        TicketSettlement settlement;
        try
        {
            settlement = await HandleAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            // Shutting down: leaving the message unsettled returns it to the subscription.
            return;
        }
        catch (Exception error)
        {
            Logger.LogError("Handling a {Subscription} message failed ({Error}).", Subscription, error.GetType().Name);
            settlement = TicketSettlement.Abandon;
        }

        switch (settlement)
        {
            case TicketSettlement.Complete:
                await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
                break;
            case TicketSettlement.DeadLetter:
                await args.DeadLetterMessageAsync(args.Message,
                    deadLetterReason: "UnsupportedTicketMessage",
                    deadLetterErrorDescription: "The message cannot become valid on retry.",
                    args.CancellationToken).ConfigureAwait(false);
                break;
            default:
                await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        // Broker-level errors carry no per-message data.
        Logger.LogError("Service Bus {Subscription} error during {Operation} ({Error}).",
            Subscription, args.ErrorSource, args.Exception.GetType().Name);
        return Task.CompletedTask;
    }
}

// Worker-side processor for ticket commands. Rendering is serialized inside the renderer, so one
// in flight per process is the honest concurrency, and the lock must outlive a full Chromium render.
public sealed class TicketGenerationProcessor(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    ILogger<TicketGenerationProcessor> logger)
    : TicketMessageProcessorBase(client, CreateOptions(), ServiceBusTopology.GeneratorSubscription, logger)
{
    private static ServiceBusProcessorOptions CreateOptions() => new()
    {
        MaxConcurrentCalls = 1,
        AutoCompleteMessages = false,
        MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(10),
        PrefetchCount = 0
    };

    protected override async Task<TicketSettlement> HandleAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        if (!TryRead<CommandMessage>(message, IsValid, out var command))
        {
            return TicketSettlement.DeadLetter;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TicketGenerationService>();
        var result = await service.GenerateAsync(command!, cancellationToken);
        return result.Outcome switch
        {
            TicketGenerationOutcome.Completed => TicketSettlement.Complete,
            TicketGenerationOutcome.Terminal => TicketSettlement.DeadLetter,
            _ => TicketSettlement.Abandon
        };
    }

    private static bool IsValid(CommandMessage message) =>
        message.Subject == TicketMessageSubjects.GenerateTicket && message.Version == TicketMessageContract.Version;
}

// API-side processor for ticket-generated events, with a fresh application and EF scope per event.
public sealed class TicketPersistenceProcessor(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    ILogger<TicketPersistenceProcessor> logger)
    : TicketMessageProcessorBase(client, CreateOptions(), ServiceBusTopology.PersistenceSubscription, logger)
{
    private static ServiceBusProcessorOptions CreateOptions() => new()
    {
        // Metadata writes are short SQL transactions, so a few may overlap safely.
        MaxConcurrentCalls = 4,
        AutoCompleteMessages = false,
        MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(2),
        PrefetchCount = 0
    };

    protected override async Task<TicketSettlement> HandleAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        if (!TryRead<EventMessage>(message, IsValid, out var completion))
        {
            return TicketSettlement.DeadLetter;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TicketPersistenceService>();
        var result = await service.HandleAsync(completion!, cancellationToken);
        return result.Outcome switch
        {
            TicketPersistenceOutcome.Saved or TicketPersistenceOutcome.Superseded or TicketPersistenceOutcome.Rejected
                => TicketSettlement.Complete,
            TicketPersistenceOutcome.Terminal => TicketSettlement.DeadLetter,
            _ => TicketSettlement.Abandon
        };
    }

    private static bool IsValid(EventMessage message) =>
        message.Subject == TicketMessageSubjects.TicketGenerated && message.Version == TicketMessageContract.Version;
}
