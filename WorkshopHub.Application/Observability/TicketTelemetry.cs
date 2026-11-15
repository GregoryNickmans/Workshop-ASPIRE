using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WorkshopHub.Application.Observability;

// Counts processing attempts, including retries and already-completed work, not unique tickets.
// The host's meter factory owns the meter lifetime; Application needs no OpenTelemetry dependency.
public sealed class TicketTelemetry
{
    public static readonly ActivitySource Source = new("WorkshopHub.Tickets");
    private readonly Counter<long> operations;
    private readonly Histogram<double> duration;

    public TicketTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("WorkshopHub.Tickets");
        operations = meter.CreateCounter<long>("tickets.operations", description: "Ticket processing attempts by stage and outcome.");
        duration = meter.CreateHistogram<double>("tickets.operation.duration", "s", "Duration of one ticket processing attempt, excluding queue time.");
    }

    public void Record(string stage, string outcome, TimeSpan elapsed)
    {
        // Explicitly bound cardinality. Never pass identifiers, payloads or exception messages here.
        var supported = stage switch
        {
            "request" => outcome is "queued" or "ready" or "rejected" or "failed" or "cancelled",
            "generate" => outcome is "completed" or "transient" or "terminal" or "failed" or "cancelled",
            "persist" => outcome is "saved" or "superseded" or "rejected" or "transient" or "terminal" or "failed" or "cancelled",
            _ => false
        };
        if (!supported) { throw new ArgumentException("Unsupported ticket stage/outcome pair."); }

        var tags = new TagList { { "ticket.stage", stage }, { "ticket.outcome", outcome } };
        operations.Add(1, tags);
        duration.Record(elapsed.TotalSeconds, tags);
    }
}
