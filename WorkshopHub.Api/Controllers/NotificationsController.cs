using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Api.Notifications;

namespace WorkshopHub.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(BookingNotificationPublisher notifications, IHostEnvironment environment)
    : ControllerBase
{
    // Anonymous local sample only. Production needs validated identity and authorized user/groups.
    [HttpPost("negotiate")]
    public async Task<IActionResult> Negotiate(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!environment.IsDevelopment()) { return NotFound(); }
        return Ok(await notifications.NegotiateAsync(cancellationToken));
    }
}
