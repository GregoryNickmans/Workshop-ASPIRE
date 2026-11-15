using WorkshopHub.Infrastructure;
using WorkshopHub.Infrastructure.Persistence;
using WorkshopHub.Seeder;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment() || !builder.Configuration.GetValue<bool>("Workshop:EnableSeeding"))
{
    throw new InvalidOperationException("Seeder is only available in an explicitly enabled local Development run.");
}

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("database")
    ?? throw new InvalidOperationException("Run through WorkshopHub.AppHost; connection 'database' is required.");
builder.Services.AddWorkshopSqlServerPersistence(connectionString);
builder.EnrichSqlServerDbContext<DatabaseContext>();

builder.Services.AddScoped<SeedDataset>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapDefaultEndpoints();

app.MapPost("/seed", async (SeedDataset dataset, CancellationToken token) =>
    Results.Ok(await dataset.ApplyAsync(token)));

app.Run();
