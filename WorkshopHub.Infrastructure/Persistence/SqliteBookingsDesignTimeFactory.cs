using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkshopHub.Infrastructure.Persistence;

public sealed class SqliteBookingsDesignTimeFactory : IDesignTimeDbContextFactory<SqliteBookingsDbContext>
{
    public SqliteBookingsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqliteBookingsDbContext>()
        .UseSqlite("Data Source=workshophub-design.db").Options);
}
