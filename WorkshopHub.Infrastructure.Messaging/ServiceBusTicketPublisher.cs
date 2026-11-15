using Azure.Messaging.ServiceBus;
using ErrorOr;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Contracts.Messaging;

namespace WorkshopHub.Infrastructure.Messaging;

// Thin Service Bus sender adapter. Both directions share one topic; the subject on each message is what
// the broker's subscription rules filter on, so the sender never has to pick a destination.
public sealed class ServiceBusTicketPublisher(ServiceBusClient client, ILogger<ServiceBusTicketPublisher> logger)
    : ITicketRequestPublisher, ITicketCompletionPublisher
{
    // The SDK's own send timeout is far too patient for a request path: approval has already committed,
    // so a queued ticket must fail fast into a warning instead of holding the HTTP call open.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    // The worker has no user waiting, but a stuck publish must still abandon the message and retry.
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(15);

    public Task<ErrorOr<Success>> PublishAsync(CommandMessage message, CancellationToken cancellationToken) =>
        SendAsync(message.Subject, TicketMessages.Serialize(message), RequestTimeout, cancellationToken);

    public Task<ErrorOr<Success>> PublishAsync(EventMessage message, CancellationToken cancellationToken) =>
        SendAsync(message.Subject, TicketMessages.Serialize(message), CompletionTimeout, cancellationToken);

    private async Task<ErrorOr<Success>> SendAsync(string subject, string body, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);
        try
        {
            await using var sender = client.CreateSender(ServiceBusTopology.Topic);
            // Subject is the routing key the subscription rules filter on; MessageId ties a redelivery to
            // the same work.
            await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString(body))
            {
                Subject = subject,
                ContentType = "application/json"
            }, budget.Token);
            return Result.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // A bounded timeout is a broker problem, not a caller cancellation.
            logger.LogWarning("Publishing a {Subject} message to topic '{Topic}' timed out after {Timeout}.",
                subject, ServiceBusTopology.Topic, timeout);
            return Error.Failure("Ticket.PublishTimedOut", "The broker did not accept the message in time.");
        }
        catch (Exception error)
        {
            // Log without the body: it carries attendee data.
            logger.LogWarning("Publishing a {Subject} message to topic '{Topic}' failed ({Error}).",
                subject, ServiceBusTopology.Topic, error.GetType().Name);
            return Error.Failure("Ticket.PublishFailed", "The message could not be sent to the broker.");
        }
    }
}
