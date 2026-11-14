using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Application.Services;
using WorkshopHub.Contracts.Bookings;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Api.Controllers;

[ApiController]
[Route("api/v1/bookings")]
public sealed class BookingsController(BookingService bookingService) : WorkshopController
{
    // Only the submitter receives this receipt. No public contact-bearing list/detail endpoint.
    [HttpPost]
    public async Task<IActionResult> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await bookingService.CreateAsync(new CourseId(request.CourseId), request.ParticipantName, request.ParticipantEmail, cancellationToken);
        return result.IsError ? Failure(result.Errors) : Created($"/api/v1/admin/bookings/{result.Value.Id.Value}", Receipt(result.Value));
    }
}
