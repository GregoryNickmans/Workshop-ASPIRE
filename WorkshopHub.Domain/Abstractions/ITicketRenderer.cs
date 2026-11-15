using ErrorOr;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Domain.Abstractions;

// PDF generation port. Infrastructure supplies the browser-backed implementation.
public interface ITicketRenderer
{
    Task<ErrorOr<byte[]>> RenderAsync(TicketDocument document, CancellationToken cancellationToken);
}
