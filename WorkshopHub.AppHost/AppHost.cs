var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddSqlServer("sqlServer")
    .WithDataVolume()
    .AddDatabase("database");

var backendApi = builder.AddProject<Projects.WorkshopHub_Api>("backendApi")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.WorkshopHub_Web>("frontend")
    .WithReference(backendApi)
    .WaitFor(backendApi)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
