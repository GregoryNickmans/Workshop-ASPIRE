namespace WorkshopHub.Contracts.Messaging;

// Names shared by the AppHost (topic, subscriptions, rules), the API and the worker. One ticket topic
// carries both directions; each subscription sees only its own subject.
public static class ServiceBusTopology
{
    public const string ConnectionName = "serviceBus";
    public const string Topic = "ticket";
    public const string GeneratorSubscription = "ticket-generator";
    public const string PersistenceSubscription = "ticket-persistence";
}

// Subjects double as the broker filter for each subscription, so a command can never reach the
// persistence subscription and an event can never reach the generator.
public static class TicketMessageSubjects
{
    public const string GenerateTicket = "GenerateTicket";
    public const string TicketGenerated = "TicketGenerated";
}
