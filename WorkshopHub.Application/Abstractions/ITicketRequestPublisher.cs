using ErrorOr;
using WorkshopHub.Contracts.Messaging;

namespace WorkshopHub.Application.Abstractions;

// Submission port. The approval path awaits only this call, never rendering or storage.
public interface ITicketRequestPublisher
{
    // Returns a failure when the broker could not accept the command; the ticket itself is unaffected.
    Task<ErrorOr<Success>> PublishAsync(CommandMessage message, CancellationToken cancellationToken);
}
