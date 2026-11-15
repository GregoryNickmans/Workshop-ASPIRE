using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using WorkshopHub.Domain.Abstractions;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Tickets;

// Renders exactly one ticket at a time and tears the browser down afterwards.
// A per-render lifecycle keeps memory flat and avoids a long-lived browser pool in this workshop.
public sealed class PlaywrightTicketRenderer(ILogger<PlaywrightTicketRenderer> logger) : ITicketRenderer
{
    private static readonly SemaphoreSlim RenderGate = new(1, 1);
    private static readonly TimeSpan TotalBudget = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PlaywrightOutcomeBudget = TimeSpan.FromSeconds(50);

    public async Task<ErrorOr<byte[]>> RenderAsync(TicketDocument document, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TotalBudget);

        // Serialize renders: each one owns a full browser lifecycle, so concurrency multiplies memory.
        await RenderGate.WaitAsync(budget.Token);
        try
        {
            return await RenderCoreAsync(TicketTemplate.Render(document), budget.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Error.Failure("Ticket.Cancelled", "Ticket generation was cancelled.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Ticket PDF rendering exceeded its time budget.");
            return Error.Failure("Ticket.Timeout", "Ticket generation took too long. Try again.");
        }
        catch (Exception error)
        {
            // A missing or broken Chromium install surfaces here; no ticket row may be created from it.
            logger.LogError(error, "Ticket PDF rendering failed.");
            return Error.Failure("Ticket.RenderFailed", "The ticket could not be generated. Verify the Chromium browser is installed.");
        }
        finally
        {
            RenderGate.Release();
        }
    }

    private async Task<ErrorOr<byte[]>> RenderCoreAsync(string html, CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync().WaitAsync(PlaywrightOutcomeBudget, cancellationToken);
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--disable-background-networking"]
        }).WaitAsync(PlaywrightOutcomeBudget, cancellationToken);

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false,
            Offline = true,
            Locale = "en-GB",
            TimezoneId = "UTC"
        }).WaitAsync(PlaywrightOutcomeBudget, cancellationToken);

        try
        {
            // Block every non-document request: the ticket is fully self-contained, so nothing may fetch.
            await context.RouteAsync("**/*", route => route.Request.IsNavigationRequest
                ? Task.CompletedTask
                : route.AbortAsync());

            var page = await context.NewPageAsync().WaitAsync(PlaywrightOutcomeBudget, cancellationToken);
            await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load })
                .WaitAsync(PlaywrightOutcomeBudget, cancellationToken);

            var pdf = await page.PdfAsync(new PagePdfOptions
            {
                Format = "A4",
                PrintBackground = true,
                PreferCSSPageSize = true,
                Margin = new Margin { Top = "0", Bottom = "0", Left = "0", Right = "0" }
            }).WaitAsync(PlaywrightOutcomeBudget, cancellationToken);

            if (pdf.Length == 0 || !HasPdfHeader(pdf))
            {
                return Error.Failure("Ticket.RenderFailed", "The renderer produced no valid PDF document.");
            }
            return pdf;
        }
        finally
        {
            // Close the context before its browser; dispose failures must not mask the render result.
            try { await context.CloseAsync(); } catch (Exception error) { logger.LogDebug(error, "Closing the ticket browser context failed."); }
        }
    }

    private static bool HasPdfHeader(byte[] pdf) =>
        pdf.Length > 4 && pdf[0] == (byte)'%' && pdf[1] == (byte)'P' && pdf[2] == (byte)'D' && pdf[3] == (byte)'F';
}
