using System.Security.Cryptography;
using System.Text;

namespace WorkshopHub.Domain.ValueObjects;

public readonly record struct TicketId(Guid Value)
{
    // One ticket per booking: the same booking always names the same ticket, so a retried or repeated
    // submission can never create a second candidate. The fixed namespace keeps this from being a bare
    // hash of the booking reference.
    private static readonly Guid Namespace = new("c9f0c1c7-4d5b-4a8e-9a1c-6f1b2f0b8a10");

    public static TicketId ForBooking(BookingId bookingId)
    {
        if (bookingId.Value == Guid.Empty) { return default; }

        Span<byte> input = stackalloc byte[32];
        Namespace.TryWriteBytes(input[..16]);
        bookingId.Value.TryWriteBytes(input[16..]);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        var bytes = hash[..16].ToArray();
        // RFC 4122 variant and a "custom" version nibble, so the value is a well-formed GUID.
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new TicketId(new Guid(bytes));
    }

    public override string ToString() => Value.ToString("D");
}
