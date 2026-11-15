using System.Text.Json;

namespace WorkshopHub.Contracts.Messaging;

// One JSON shape for both directions, shared by the publisher and both processors. Serialized to text
// so the contracts assembly needs no Azure or broker dependency.
public static class TicketMessages
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T message) => JsonSerializer.Serialize(message, Options);

    // Throws JsonException for a body that is not this message, so the processor can dead-letter it.
    public static T? Deserialize<T>(string body) => JsonSerializer.Deserialize<T>(body, Options);
}
