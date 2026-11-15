using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Tickets;

// Fills the embedded ticket template. Every dynamic value is HTML-encoded, so a course title or
// attendee name can never inject markup, a style rule or a network reference into the page.
internal static partial class TicketTemplate
{
    private const string ResourceName = "WorkshopHub.Infrastructure.Tickets.Templates.TicketTemplate.html";

    private static readonly Lazy<string> Template = new(Load);

    public static string Render(TicketDocument document) =>
        Placeholder().Replace(Template.Value, match => match.Groups["name"].Value switch
        {
            "CourseTitle" => Encode(document.CourseTitle),
            "AttendeeName" => Encode(document.AttendeeName),
            "Price" => Encode(FormatPrice(document.PriceCents)),
            "IssuedAt" => Encode(FormatIssued(document.IssuedAt)),
            "TicketId" => Encode(document.TicketId.ToString("D")),
            "BookingId" => Encode(document.BookingId.ToString("D")),
            var unknown => throw new InvalidOperationException($"Unknown ticket template placeholder '{unknown}'.")
        });

    private static string Load()
    {
        using var stream = typeof(TicketTemplate).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded ticket template '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    // nl-BE number formatting matches the website's DisplayFormatting.Price, independent of server culture.
    private static string FormatPrice(int priceCents) =>
        $"{(priceCents / 100m).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("nl-BE"))} EUR";

    private static string FormatIssued(DateTimeOffset issuedAt) =>
        issuedAt.UtcDateTime.ToString("dd MMM yyyy · HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\{\{(?<name>[A-Za-z]+)\}\}")]
    private static partial Regex Placeholder();
}
