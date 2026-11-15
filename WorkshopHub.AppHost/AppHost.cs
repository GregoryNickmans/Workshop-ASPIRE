using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddSqlServer("sqlServer")
    .WithDataVolume()
    .AddDatabase("database");

// Persistent Azurite so locally generated ticket PDFs survive AppHost restarts.
// The logical resource is "Tickets"; Azure requires a lowercase physical container name.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(container =>
    {
        container.WithDataVolume();
        container.WithBlobPort(10000);
    });
var tickets = storage.AddBlobContainer("tickets");

var migrations = builder.AddProject<Projects.WorkshopHub_Migrator>("migrations")
    .WithReference(database)
    .WaitFor(database);

var backendApi = builder.AddProject<Projects.WorkshopHub_Api>("backendApi")
    .WithReference(database)
    .WaitFor(database)
    .WaitForCompletion(migrations)
    .WithReference(tickets)
    .WaitFor(storage)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.WorkshopHub_Web>("frontend")
    .WithReference(backendApi)
    .WaitFor(backendApi)
    .WithHttpHealthCheck("/health");

if (builder.ExecutionContext.IsRunMode && builder.Environment.IsDevelopment())
{
    builder.AddProject<Projects.WorkshopHub_Seeder>("seeder")
        .WithReference(database)
        .WaitFor(database)
        .WaitForCompletion(migrations)
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("Workshop__EnableSeeding", "true")
        .WithHttpEndpoint(name: "http")
        .WithHttpHealthCheck("/health")
        .WithHttpCommand("/seed", "Reset and seed", endpointName: "http", commandOptions: new()
        {
            Method = HttpMethod.Post,
            IconName = "Sparkle",
            ResultMode = HttpCommandResultMode.Json,
            ConfirmationMessage = "Drop the entire database, apply all migrations, and restore the original course catalog? All existing data will be lost."
        });
}

builder.Build().Run();
