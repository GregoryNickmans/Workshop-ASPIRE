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
        services.AddScoped<TicketService>();
        return services;
    }

    // AppHost supplies the SQL Server connection; store registrations stay unchanged.
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
        services.AddScoped<TicketQueryExecutor>();
        services.AddScoped<ITicketQueryExecutor>(provider => provider.GetRequiredService<TicketQueryExecutor>());
        services.AddScoped<ITicketStore, TicketStore>();
        return services;
    }
}
