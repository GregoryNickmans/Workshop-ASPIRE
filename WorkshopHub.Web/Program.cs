using WorkshopHub.Web.Components;
using WorkshopHub.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults provides a set of opinionated defaults for hosting and configuring the application.
builder.AddServiceDefaults();

// Published deployments already contain these assets; this loads the manifest when running locally.
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<DisplayFormatting>();
builder.Services.AddHttpClient<BookingApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://backendApi");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { error = "Check API readiness and persisted state before retrying." }, context.RequestAborted);
}));
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();
app.Run();
