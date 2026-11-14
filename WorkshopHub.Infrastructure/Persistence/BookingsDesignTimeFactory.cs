using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkshopHub.Infrastructure.Persistence;

// Tooling only: no secrets or database connection required for scaffolding.
public sealed class BookingsDesignTimeFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<DatabaseContext>()
        .UseSqlServer("Server=localhost;Database=WorkshopHubDesign;Integrated Security=true;TrustServerCertificate=true").Options);
}
