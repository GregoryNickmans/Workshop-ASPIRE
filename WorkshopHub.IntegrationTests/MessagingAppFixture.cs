using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace WorkshopHub.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MessagingCollection : ICollectionFixture<MessagingAppFixture>
{
    public const string Name = "Aspire messaging";
}

public sealed class MessagingAppFixture : IAsyncLifetime
{
    private IDistributedApplicationTestingBuilder? _builder;
    private DistributedApplication? _app;
    private HttpClient? _api;

    public HttpClient Api => _api ?? throw new InvalidOperationException("Aspire has not started.");

    public async Task InitializeAsync()
    {
        using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            _builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.WorkshopHub_AppHost>(startup.Token);
            IsolateResources(_builder);
            // Retain the log backlog for failure diagnostics without streaming emulator credentials.
            _builder.Services.AddLogging(logging => logging.AddFilter("WorkshopHub.AppHost.Resources", LogLevel.None));
            _app = await _builder.BuildAsync(startup.Token);
            await _app.StartAsync(startup.Token);
            await Task.WhenAll(
                _app.ResourceNotifications.WaitForResourceHealthyAsync("backendApi", startup.Token),
                _app.ResourceNotifications.WaitForResourceHealthyAsync("serviceBusProcessor", startup.Token));
            _api = _app.CreateHttpClient("backendApi", "http");
            _api.Timeout = TimeSpan.FromSeconds(30);
        }
        catch (Exception error)
        {
            var diagnostics = await GetDiagnosticsAsync();
            await DisposeAsync();
            throw new InvalidOperationException($"Aspire messaging startup failed.\n{diagnostics}", error);
        }
    }

    private static void IsolateResources(IDistributedApplicationTestingBuilder builder)
    {
        // Azure emulator wrappers share annotations with their logical resources; they are not
        // necessarily ContainerResources in the model. Inspect every resource, not only containers.
        foreach (var resource in builder.Resources)
        {
            foreach (var mount in resource.Annotations.OfType<ContainerMountAnnotation>()
                         .Where(mount => mount.Type == ContainerMountType.Volume).ToArray())
            {
                resource.Annotations.Remove(mount);
            }
            foreach (var name in resource.Annotations.OfType<ContainerNameAnnotation>().ToArray())
            {
                resource.Annotations.Remove(name);
            }
            foreach (var endpoint in resource.Annotations.OfType<EndpointAnnotation>().Where(endpoint => endpoint.IsProxied))
            {
                endpoint.Port = null;
            }

            // Use native wrappers so WithLifetime updates both persistence annotations correctly.
            ContainerResource? container = resource switch
            {
                ContainerResource native => native,
                AzureStorageResource storage when storage.IsEmulator => new AzureStorageEmulatorResource(storage),
                AzureServiceBusResource bus when bus.IsEmulator => new AzureServiceBusEmulatorResource(bus),
                AzureSignalRResource signalR when signalR.IsEmulator => new AzureSignalREmulatorResource(signalR),
                _ => null
            };
            if (container is not null)
            {
                builder.CreateResourceBuilder(container).WithLifetime(ContainerLifetime.Session);
            }
            if (resource is ProjectResource project)
            {
                builder.CreateResourceBuilder(project)
                    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");
            }
        }

        foreach (var resource in builder.Resources)
        {
            Assert.DoesNotContain(resource.Annotations.OfType<ContainerMountAnnotation>(),
                mount => mount.Type == ContainerMountType.Volume);
            Assert.DoesNotContain(resource.Annotations.OfType<EndpointAnnotation>(),
                endpoint => endpoint.IsProxied && endpoint.Port is not null);
            Assert.DoesNotContain(resource.Annotations.OfType<ContainerLifetimeAnnotation>(),
                lifetime => lifetime.Lifetime == ContainerLifetime.Persistent);
        }
    }

    public async Task<string> GetDiagnosticsAsync()
    {
        if (_app is null || _builder is null) { return "Application not built yet."; }
        var output = new StringBuilder();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var logs = _app.Services.GetRequiredService<ResourceLoggerService>();
            foreach (var resource in _builder.Resources.Where(resource =>
                         resource is ProjectResource || resource.Annotations.OfType<ContainerImageAnnotation>().Any()))
            {
                var state = _app.ResourceNotifications.TryGetCurrentState(resource.Name, out var update)
                    ? update.Snapshot.State?.Text ?? "Unknown"
                    : "Unknown";
                output.AppendLine($"{resource.Name}: {state}");
                var recent = new Queue<string>();
                await foreach (var batch in logs.GetAllAsync(resource).WithCancellation(budget.Token))
                {
                    foreach (var line in batch)
                    {
                        // Do not copy credentials from infrastructure logs into test output.
                        var content = line.Content;
                        if (new[] { "password", "token", "secret", "connectionstring", "connection string", "AccountKey", "AccessKey", "SharedAccess", "Authorization" }
                            .Any(term => content.Contains(term, StringComparison.OrdinalIgnoreCase)))
                        {
                            content = "[credential-related log line omitted]";
                        }
                        recent.Enqueue(content.Length > 1000 ? content[..1000] : content);
                        if (recent.Count > 20) { recent.Dequeue(); }
                    }
                }
                foreach (var line in recent) { output.AppendLine(line); }
            }
        }
        catch (Exception error)
        {
            output.AppendLine($"Diagnostics incomplete: {error.GetType().Name}");
        }
        return output.ToString();
    }

    public async Task DisposeAsync()
    {
        _api?.Dispose();
        _api = null;
        try
        {
            if (_app is not null) { await _app.DisposeAsync(); }
        }
        finally
        {
            _app = null;
            if (_builder is not null)
            {
                await _builder.DisposeAsync();
                _builder = null;
            }
        }
    }
}
