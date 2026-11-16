using WorkshopHub.Web.Services;

namespace WorkshopHub.Web.Endpoints;

public static class BookingNotifications
{
    public static IEndpointRouteBuilder MapBookingNotifications(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/notifications/negotiate", async (BookingApiClient api, HttpContext context,
            IHostEnvironment environment) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!environment.IsDevelopment()) { return Results.NotFound(); }
            // Same-origin browser POST only; no cookies, browser identity, API address or management key.
            if (context.Request.Headers.Origin.Count != 1 ||
                !Uri.TryCreate(context.Request.Headers.Origin.ToString(), UriKind.Absolute, out var origin) ||
                !string.Equals(origin.GetLeftPart(UriPartial.Authority),
                    $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            try { return Results.Ok(await api.NegotiateNotificationsAsync(context.RequestAborted)); }
            catch (BookingApiException error) { return Results.StatusCode((int)error.StatusCode); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                return Results.Problem(statusCode: 502, title: "Notifications unavailable. Refresh records manually.");
            }
        });
        return endpoints;
    }
}
