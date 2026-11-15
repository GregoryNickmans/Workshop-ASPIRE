using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkshopHub.Infrastructure;
using WorkshopHub.Infrastructure.Persistence;
using WorkshopHub.Migrator;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("database")
    ?? throw new InvalidOperationException("Run through WorkshopHub.AppHost; connection 'database' is required.");
builder.Services.AddWorkshopSqlServerPersistence(connectionString);
builder.EnrichSqlServerDbContext<DatabaseContext>();
builder.Services.AddHostedService<MigrationWorker>();

await builder.Build().RunAsync();
