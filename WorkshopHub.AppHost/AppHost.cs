var builder = DistributedApplication.CreateBuilder(args);

var backendApi = builder.AddProject<Projects.WorkshopHub_Api>("backendApi")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.WorkshopHub_Web>("frontend")
    .WithReference(backendApi)
    .WaitFor(backendApi)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
