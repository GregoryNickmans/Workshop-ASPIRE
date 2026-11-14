using Microsoft.AspNetCore.Http.Features;
using Swashbuckle.AspNetCore.SwaggerUI;
using WorkshopHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = (1024 * 1024) + (64 * 1024));

var databasePath = Path.GetFullPath(builder.Configuration["Storage:DatabasePath"] ?? "App_Data/workshophub.db", builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
builder.Services.AddWorkshopApplicationServices();
builder.Services.AddWorkshopSqlitePersistence(databasePath);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "WorkshopHub API v1");
        options.DocExpansion(DocExpansion.List);
    });
}

app.MapControllers();
app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
