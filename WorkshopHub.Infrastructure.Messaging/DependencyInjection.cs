using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Contracts.Messaging;

namespace WorkshopHub.Infrastructure.Messaging;

public static class DependencyInjection
{
    // The broker client is bound to the Aspire "serviceBus" reference for both hosts.
    // The SDK's defaults allow a single attempt to hang for a minute when the broker host is
    // unreachable, and a cancellation token does not interrupt an in-flight AMQP connect. Approval
    // must never hold an HTTP call open that long, so the attempt itself is bounded here, and retry
    // responsibility stays with broker redelivery and the caller's explicit retry.
    public static IHostApplicationBuilder AddWorkshopServiceBusClient(this IHostApplicationBuilder builder)
    {
        // Aspire subscribes to the SDK source, but Service Bus requires this switch to emit it.
        // Enable before constructing clients so sends and processor callbacks carry W3C context.
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
        builder.AddAzureServiceBusClient(ServiceBusTopology.ConnectionName, configureClientBuilder: client =>
            client.ConfigureOptions(options =>
            {
                options.RetryOptions.TryTimeout = TimeSpan.FromSeconds(5);
                options.RetryOptions.MaxRetries = 1;
                options.RetryOptions.Mode = ServiceBusRetryMode.Fixed;
            }));
        return builder;
    }

    // Submission side: the API's approval and manual-ticket paths use this, nothing more.
    public static IHostApplicationBuilder AddWorkshopTicketPublisher(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ServiceBusTicketPublisher>();
        builder.Services.AddSingleton<ITicketRequestPublisher>(provider => provider.GetRequiredService<ServiceBusTicketPublisher>());
        return builder;
    }

    // Worker side: publishes completion events and runs the command processor.
    public static IHostApplicationBuilder AddWorkshopTicketGeneration(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ServiceBusTicketPublisher>();
        builder.Services.AddSingleton<ITicketCompletionPublisher>(provider => provider.GetRequiredService<ServiceBusTicketPublisher>());
        builder.Services.AddSingleton(provider => new TicketGenerationProcessor(
            provider.GetRequiredService<ServiceBusClient>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<TicketGenerationProcessor>>()));
        builder.Services.AddHostedService<TicketGenerationHost>();
        return builder;
    }

    // API side: a hosted processor for completion events. Every event gets a fresh application scope.
    public static IHostApplicationBuilder AddWorkshopTicketPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(provider => new TicketPersistenceProcessor(
            provider.GetRequiredService<ServiceBusClient>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<TicketPersistenceProcessor>>()));
        builder.Services.AddHostedService<TicketPersistenceHost>();
        return builder;
    }
}
