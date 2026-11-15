using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkshopHub.Application.Observability;
using WorkshopHub.Application.Services;
using WorkshopHub.Infrastructure.Messaging;
using WorkshopHub.Infrastructure.Tickets;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults gives this worker health endpoints and telemetry like the other hosts, so the
// dashboard can show it as Ready rather than guessing from a process.
builder.AddServiceDefaults();

// The worker needs the broker, private storage and a browser. It deliberately has no reference to the
// API and no SQL connection: metadata is the API's responsibility, reported back as an event.
builder.AddWorkshopServiceBusClient();
builder.AddWorkshopTicketStorage();
builder.AddWorkshopTicketRenderer();

builder.Services.TryAddSingleton<TicketTelemetry>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TicketGenerationService>();
builder.AddWorkshopTicketGeneration();

var app = builder.Build();

// The private container must exist before the first command is handled.
await app.Services.EnsureTicketStorageAsync();

app.MapDefaultEndpoints();
await app.RunAsync();
