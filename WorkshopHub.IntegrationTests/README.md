# Messaging integration tests

These xUnit tests launch the real `WorkshopHub.AppHost` using `Aspire.Hosting.Testing`. They confirm a booking through REST and verify the Service Bus command, worker PDF generation/upload, completion event, SQL ticket metadata, and downloadable PDF. A repeated ready-ticket request must retain the same metadata and PDF. SignalR refresh hints and browser UI are not part of the assertions.

## Prerequisites

- .NET SDK matching the solution's `global.json` (.NET 10).
- A running Docker-compatible container runtime (Docker or Podman), with capacity for two SQL Server containers and the Azure emulators.
- Playwright-compatible Chromium installed for the account running the worker.

Run from the solution root:

```powershell
dotnet restore WorkshopHub.sln
dotnet build WorkshopHub.sln --no-restore
pwsh -File ./WorkshopHub.ServiceBusProcessor/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test ./WorkshopHub.IntegrationTests/WorkshopHub.IntegrationTests.csproj --no-build --logger "console;verbosity=detailed"
```

The test is tagged `Category=Integration`; `dotnet test WorkshopHub.sln --filter "Category=Integration"` also runs it. Running all solution tests requires the same infrastructure prerequisites.

On Linux, use `install --with-deps chromium` with the generated Playwright script to install required browser system dependencies. For Release builds, use the matching `bin/Release/net10.0` script and `dotnet test -c Release`.

## Isolation and failures

The test fixture removes data-volume mounts, uses session container lifetimes and dynamic host ports, and preserves required emulator configuration. It does not invoke the Seeder or access developer volumes. All application wiring and real consumers remain in place. Test data is created through REST with unique synthetic identifiers.

Startup/readiness is bounded to three minutes; the test flow has a 90-second budget. Only `404 Ticket.NotFound` is retried while awaiting ticket persistence; mutating requests are never automatically retried. Failure output includes the last HTTP response, phase, resource states and bounded log tails (credential-related lines are omitted).

Tests sharing the messaging collection run serially. The fixture disposes its HTTP client and Aspire application after the collection, including cleanup on startup failure. Missing infrastructure or Chromium causes a failing test, not a silently skipped test.
