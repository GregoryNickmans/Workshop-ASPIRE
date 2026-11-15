using System.Net;
using System.Text.RegularExpressions;

namespace WorkshopHub.Web.Endpoints;

// Same-origin download route. The browser never learns the API address or the private storage location:
// the bytes are relayed by this host while the upstream response is still alive.
public static partial class TicketDownloads
{
    public const string Route = "/admin/tickets/{ticketId:guid}/download";

    // The only download URL the UI should ever build, so the route exists in one place.
    public static string Url(Guid ticketId) => $"/admin/tickets/{ticketId:D}/download";

    private const string PdfContentType = "application/pdf";

    public static IEndpointRouteBuilder MapTicketDownloads(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, DownloadAsync);
        return endpoints;
    }

    internal static async Task DownloadAsync(Guid ticketId, Services.BookingApiClient bookingApi, HttpContext context)
    {
        // Tied to the browser connection, so a client disconnect aborts the upstream call and the copy.
        var cancellationToken = context.RequestAborted;
        HttpResponseMessage upstream;
        try
        {
            // Headers only: a failure status is still reportable as JSON at this point.
            upstream = await bookingApi.OpenTicketPdfAsync(ticketId, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await WriteFailureAsync(context, error);
            return;
        }

        using var response = upstream;
        try
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = PdfContentType;
            context.Response.Headers.ContentDisposition = $"attachment; filename=\"{AttachmentName(ticketId, response)}\"";
            context.Response.Headers.CacheControl = "no-store";
            if (response.Content.Headers.ContentLength is { } length) { context.Response.ContentLength = length; }

            // The upstream stream stays open for the duration of the copy and is disposed with `response`.
            await using var pdf = await response.Content.ReadAsStreamAsync(cancellationToken);
            await pdf.CopyToAsync(context.Response.Body, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The browser went away mid-download; there is no one left to report to.
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Headers and possibly bytes are already sent, so the status can no longer be changed.
            context.Abort();
        }
    }

    // Only reached before the first PDF byte, so a real status and details can still be sent.
    private static async Task WriteFailureAsync(HttpContext context, Exception error)
    {
        var (status, code, message) = error switch
        {
            Services.BookingApiException api => ((int)api.StatusCode, api.Code ?? "Ticket.Unavailable", api.Message),
            _ => ((int)HttpStatusCode.BadGateway, "Ticket.Unavailable", "The ticket could not be downloaded. Try again.")
        };
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new { status, title = code, detail = message }, CancellationToken.None);
    }

    // Prefers the API's own file name, but only when it is a plain PDF name. A GUID-based fallback keeps
    // the header free of quotes, separators and control characters even if the upstream name is unusual.
    private static string AttachmentName(Guid ticketId, HttpResponseMessage response)
    {
        var upstream = response.Content.Headers.ContentDisposition?.FileName?.Trim().Trim('"');
        return upstream is not null && SafeFileName().IsMatch(upstream) ? upstream : $"workshophub-ticket-{ticketId:N}.pdf";
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,118}\\.pdf$")]
    private static partial Regex SafeFileName();
}
