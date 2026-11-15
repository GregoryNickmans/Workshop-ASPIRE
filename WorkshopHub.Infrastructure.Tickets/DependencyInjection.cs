using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkshopHub.Domain.Abstractions;

namespace WorkshopHub.Infrastructure.Tickets;

public static class DependencyInjection
{
    // Storage only: binds the Aspire "Tickets" container reference and registers the private blob adapter.
    // Hosts that only read or write tickets (the API and the PDF worker) use this.
    public static IHostApplicationBuilder AddWorkshopTicketStorage(this IHostApplicationBuilder builder)
    {
        builder.AddAzureBlobContainerClient("Tickets");
        builder.Services.AddSingleton<ITicketBlobStore, AzureTicketBlobStore>();
        return builder;
    }

    // Adds the browser renderer. Only the worker calls this, so the API and every other host never
    // acquire a Chromium dependency.
    public static IHostApplicationBuilder AddWorkshopTicketRenderer(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ITicketRenderer, PlaywrightTicketRenderer>();
        return builder;
    }

    // Full integration: storage plus rendering. Kept for hosts that do both in one process.
    public static IHostApplicationBuilder AddWorkshopTicketIntegrations(this IHostApplicationBuilder builder) =>
        builder.AddWorkshopTicketStorage().AddWorkshopTicketRenderer();

    // The container is created once at startup so readiness never depends on the first ticket request.
    public static Task EnsureTicketStorageAsync(this IServiceProvider provider, CancellationToken cancellationToken = default) =>
        provider.GetRequiredService<ITicketBlobStore>().CreateContainerIfMissingAsync(cancellationToken);
}
