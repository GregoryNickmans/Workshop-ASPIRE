using System.Globalization;

namespace WorkshopHub.Web.Services;

public sealed class DisplayFormatting()
{
    private readonly string _currency = "EUR";
    public string Price(decimal price) => $"{price.ToString("N2", CultureInfo.GetCultureInfo("nl-BE"))} {_currency}";
    public static string Timestamp(DateTimeOffset value) => value.UtcDateTime.ToString("dd MMM yyyy · HH:mm 'UTC'", CultureInfo.InvariantCulture);
    public static string Reference(Guid id) => id.ToString("N")[..8].ToUpperInvariant();
}
