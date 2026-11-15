var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddSqlServer("sqlServer")
    .WithDataVolume()
    .AddDatabase("database");

var migrations = builder.AddProject<Projects.WorkshopHub_Migrator>("migrations")
    .WithReference(database)
    .WaitFor(database);

var backendApi = builder.AddProject<Projects.WorkshopHub_Api>("backendApi")
    .WithReference(database)
    .WaitFor(database)
    .WaitForCompletion(migrations)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.WorkshopHub_Web>("frontend")
    .WithReference(backendApi)
    .WaitFor(backendApi)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
