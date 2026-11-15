using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkshopHub.Domain.Abstractions;

namespace WorkshopHub.Infrastructure.Tickets;

public static class DependencyInjection
{
    // Registers the browser renderer and private blob adapter and binds the Aspire "Tickets"
    // container reference. Only the API host calls this, so Migrator and Seeder never acquire
    // Chromium or storage runtime dependencies.
    public static IHostApplicationBuilder AddWorkshopTicketIntegrations(this IHostApplicationBuilder builder)
    {
        builder.AddAzureBlobContainerClient("Tickets");
        builder.Services.AddSingleton<ITicketRenderer, PlaywrightTicketRenderer>();
        builder.Services.AddSingleton<ITicketBlobStore, AzureTicketBlobStore>();
        return builder;
    }

    // The container is created once at startup so readiness never depends on the first ticket request.
    public static Task EnsureTicketStorageAsync(this IServiceProvider provider, CancellationToken cancellationToken = default) =>
        provider.GetRequiredService<ITicketBlobStore>().CreateContainerIfMissingAsync(cancellationToken);
}
