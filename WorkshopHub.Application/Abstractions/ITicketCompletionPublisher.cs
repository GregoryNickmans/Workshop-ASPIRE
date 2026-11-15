using ErrorOr;
using WorkshopHub.Contracts.Messaging;

namespace WorkshopHub.Application.Abstractions;

// Worker-to-API completion port. A publish failure must leave the command unacknowledged.
public interface ITicketCompletionPublisher
{
    Task<ErrorOr<Success>> PublishAsync(EventMessage message, CancellationToken cancellationToken);
}
