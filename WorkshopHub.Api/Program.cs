using Microsoft.AspNetCore.Http.Features;
using Swashbuckle.AspNetCore.SwaggerUI;
using WorkshopHub.Infrastructure;
using WorkshopHub.Infrastructure.Persistence;
using WorkshopHub.Infrastructure.Tickets;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults provides a set of opinionated defaults for hosting and configuring the application.
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = (1024 * 1024) + (64 * 1024));

var connectionString = builder.Configuration.GetConnectionString("database")
    ?? throw new InvalidOperationException("Run through WorkshopHub.AppHost; connection 'database' is required.");
builder.Services.AddWorkshopApplicationServices();
builder.Services.AddWorkshopSqlServerPersistence(connectionString);
builder.EnrichSqlServerDbContext<DatabaseContext>();

// Blob/browser adapters stay out of Infrastructure so Migrator and Seeder never acquire them.
// Ticket endpoints are available in every environment; see AdminTicketsController for the security note.
builder.AddWorkshopTicketIntegrations();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "WorkshopHub API v1");
        options.DocExpansion(DocExpansion.List);
    });
}

// Create the private container before serving so readiness never depends on the first ticket request.
app.Services.EnsureTicketStorageAsync().GetAwaiter().GetResult();

app.MapControllers();
app.MapOpenApi();
app.MapDefaultEndpoints();

app.Run();
