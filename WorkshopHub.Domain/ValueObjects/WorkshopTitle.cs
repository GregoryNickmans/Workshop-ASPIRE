using ErrorOr;

namespace WorkshopHub.Domain.ValueObjects;

public sealed record WorkshopTitle : ValueObject
{
    private WorkshopTitle(string value) => Value = value;
    public string Value { get; }
    public static ErrorOr<WorkshopTitle> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 80)
        {
            return Error.Validation("Booking.Workshop", "Workshop must contain 1–80 characters.");
        }
        return new WorkshopTitle(value.Trim());
    }
}
