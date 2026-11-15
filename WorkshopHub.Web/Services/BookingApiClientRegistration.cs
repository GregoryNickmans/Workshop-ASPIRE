using Microsoft.Extensions.Http.Resilience;

namespace WorkshopHub.Web.Services;

public static class BookingApiClientRegistration
{
    // Ticket issuance renders a PDF synchronously, so this client needs a larger budget than the
    // portal's other calls. ServiceDefaults already adds the standard resilience handler to every
    // HttpClient; stacking a second pipeline would multiply retries and timeouts, so replace the
    // inherited one for this client only and leave the global defaults untouched.
    public static IServiceCollection AddBookingApiClient(this IServiceCollection services)
    {
        var builder = services.AddHttpClient<BookingApiClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://backendApi");
            // The renderer's own budget is 60 seconds; the client must outlast it.
            client.Timeout = TimeSpan.FromSeconds(120);
        });
#pragma warning disable EXTEXP0001 // Experimental API, scoped to this single replacement.
        builder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        builder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
            // Retrying a mutation could confirm or issue a ticket twice; reads keep the standard retries.
            options.Retry.DisableForUnsafeHttpMethods();
            // Validated as at least twice the attempt timeout, which is now far longer than the default.
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(300);
        });
        return services;
    }
}
