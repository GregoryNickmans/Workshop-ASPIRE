using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkshopHub.Infrastructure.Persistence;

namespace WorkshopHub.Migrator;

public sealed class MigrationWorker(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
                await db.Database.MigrateAsync(stoppingToken));

            logger.LogInformation("Database migrations completed successfully.");
        }
        catch (Exception exception)
        {
            // Host shutdown alone can exit successfully; signal failure to WaitForCompletion.
            Environment.ExitCode = 1;
            logger.LogError(exception, "Database migration failed.");
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
