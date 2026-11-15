namespace WorkshopHub.Contracts.Tickets;

public static class TicketRequestStatus
{
    public const string Queued = "Queued";
    public const string Ready = "Ready";
}

// The outcome of a ticket request. Only a ready request carries a ticket row and a downloadable file;
// a queued request carries the identity that will be used once the worker finishes, and must not be
// presented as an available ticket.
public sealed record TicketRequestResponse(Guid TicketId, string Status, TicketResponse? Ticket)
{
    public static TicketRequestResponse Queued(Guid ticketId) => new(ticketId, TicketRequestStatus.Queued, null);
    public static TicketRequestResponse Ready(TicketResponse ticket) => new(ticket.Id, TicketRequestStatus.Ready, ticket);
}
