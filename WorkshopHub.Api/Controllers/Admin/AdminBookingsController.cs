using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Application.Services;
using WorkshopHub.Contracts.Admin.Bookings;
using WorkshopHub.Contracts.Common;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/bookings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminBookingsController(BookingService bookingService, CourseService courseService) : WorkshopController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken, Guid? courseId = null, string? status = null,
        string? search = null, int page = 1, int pageSize = 20)
    {
        var result = await courseService.ListBookingsAsync(courseId.HasValue ? new CourseId(courseId.Value) : null,
            status, search, page, pageSize, cancellationToken);
        return result.IsError ? Failure(result.Errors) : Ok(new PagedResponse<AdminBookingResponse>(
            result.Value.Items.Select(AdminBooking).ToArray(), result.Value.TotalCount, result.Value.Page, result.Value.PageSize));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await bookingService.GetAsync(new BookingId(id), cancellationToken);
        return result.IsError ? Failure(result.Errors) : Ok(AdminBooking(result.Value));
    }
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await bookingService.ConfirmAsync(new BookingId(id), cancellationToken);
        return result.IsError ? Failure(result.Errors) : Ok(AdminBooking(result.Value));
    }
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await bookingService.CancelAsync(new BookingId(id), cancellationToken);
        return result.IsError ? Failure(result.Errors) : Ok(AdminBooking(result.Value));
    }
}
