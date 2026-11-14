using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Services;
using WorkshopHub.Infrastructure.Persistence;
using WorkshopHub.Infrastructure.Persistence.EntityFramework.Commands;
using WorkshopHub.Infrastructure.Persistence.EntityFramework.Queries;
using WorkshopHub.Infrastructure.Persistence.EntityFramework.Repositories;

namespace WorkshopHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkshopApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<BookingService>();
        services.AddScoped<CourseService>();
        return services;
    }

    // Active starter provider. Keep the derived and base context resolutions in the same scope.
    public static IServiceCollection AddWorkshopSqlitePersistence(this IServiceCollection services, string databasePath)
    {
        var sqliteConnection = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            DefaultTimeout = 2
        }.ToString();
        services.AddDbContext<SqliteBookingsDbContext>(options => options.UseSqlite(sqliteConnection));
        services.AddScoped<DatabaseContext>(provider => provider.GetRequiredService<SqliteBookingsDbContext>());
        return services.AddWorkshopEfStores();
    }

    // Dormant workshop option: runtime and concurrency behaviour require separate verification.
    public static IServiceCollection AddWorkshopSqlServerPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<DatabaseContext>(options => options.UseSqlServer(connectionString));
        return services.AddWorkshopEfStores();
    }

    private static IServiceCollection AddWorkshopEfStores(this IServiceCollection services)
    {
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<QueryExecutor>();
        services.AddScoped<IQueryExecutor>(provider => provider.GetRequiredService<QueryExecutor>());
        services.AddScoped<ICourseQueryExecutor>(provider => provider.GetRequiredService<QueryExecutor>());
        services.AddScoped<IBookingCommandStore, BookingCommandStore>();
        return services;
    }
}
