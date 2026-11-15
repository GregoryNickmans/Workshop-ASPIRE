using Aspire.Hosting.Azure;
using Microsoft.Extensions.Hosting;
using WorkshopHub.Contracts.Messaging;

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

// Ticket work travels through one topic and two filtered subscriptions: commands go to the PDF
// worker, completion events come back to the API. The emulator brings its own SQL Server instance,
// which stays entirely separate from the application database above.
var serviceBus = builder.AddAzureServiceBus(ServiceBusTopology.ConnectionName)
    .RunAsEmulator();
var topic = serviceBus.AddServiceBusTopic(ServiceBusTopology.Topic);
GeneratorSubscription(topic);
PersistenceSubscription(topic);

var migrations = builder.AddProject<Projects.WorkshopHub_Migrator>("migrations")
    .WithReference(database)
    .WaitFor(database);

var backendApi = builder.AddProject<Projects.WorkshopHub_Api>("backendApi")
    .WithReference(database)
    .WaitFor(database)
    .WaitForCompletion(migrations)
    .WithReference(tickets)
    .WaitFor(storage)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
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

// The worker is started alongside the API but the frontend never waits for it: approval returns
// immediately, and a queued ticket simply appears once generation finishes.
builder.AddProject<Projects.WorkshopHub_ServiceBusProcessor>("serviceBusProcessor")
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithReference(tickets)
    .WaitFor(storage)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health");

// A subscription with no rule only matches what the default catch-all forwards, so each one declares
// exactly one correlation rule on its own subject. Declaring a rule replaces the default rule, which
// is what keeps commands out of the persistence subscription and events out of the generator.
static void GeneratorSubscription(IResourceBuilder<AzureServiceBusTopicResource> topic) =>
    topic.AddServiceBusSubscription(ServiceBusTopology.GeneratorSubscription, ServiceBusTopology.GeneratorSubscription)
        .WithProperties(subscription => subscription.Rules.Add(new AzureServiceBusRule("GenerateTicketCommand")
        {
            CorrelationFilter = new AzureServiceBusCorrelationFilter { Subject = TicketMessageSubjects.GenerateTicket }
        }));

static void PersistenceSubscription(IResourceBuilder<AzureServiceBusTopicResource> topic) =>
    topic.AddServiceBusSubscription(ServiceBusTopology.PersistenceSubscription, ServiceBusTopology.PersistenceSubscription)
        .WithProperties(subscription => subscription.Rules.Add(new AzureServiceBusRule("TicketGeneratedEvent")
        {
            CorrelationFilter = new AzureServiceBusCorrelationFilter { Subject = TicketMessageSubjects.TicketGenerated }
        }));
