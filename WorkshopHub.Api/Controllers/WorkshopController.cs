using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Application.Models;
using WorkshopHub.Contracts.Admin.Bookings;
using WorkshopHub.Contracts.Admin.Courses;
using WorkshopHub.Contracts.Bookings;
using WorkshopHub.Contracts.Courses;

namespace WorkshopHub.Api.Controllers;

public abstract class WorkshopController : ControllerBase
{
    protected IActionResult Failure(List<Error> errors)
    {
        var status = errors[0].Type switch { ErrorType.Validation => 400, ErrorType.NotFound => 404, ErrorType.Conflict => 409, _ => 500 };
        var problem = new ProblemDetails { Status = status, Title = errors[0].Code, Detail = string.Join(" ", errors.Select(x => x.Description)) };
        problem.Extensions["errors"] = errors.Select(x => new { x.Code, x.Description }).ToArray();
        return StatusCode(status, problem);
    }
    protected static CourseResponse PublicCourse(CourseView course) => new(course.Id.Value, course.Title.Value, course.ShortDescription,
        course.PriceCents / 100m, course.MaximumParticipants, course.MaximumParticipants - course.OccupiedSeats);
    protected static AdminCourseResponse AdminCourse(CourseView course) => new(course.Id.Value, course.Title.Value, course.ShortDescription,
        course.PriceCents / 100m, course.MaximumParticipants, course.OccupiedSeats, course.IsArchived, course.Revision);
    protected static BookingResponse Receipt(BookingView booking) => new(booking.Id.Value,
        booking.CourseId?.Value ?? Guid.Empty, booking.Workshop.Value, booking.PriceCents / 100m, booking.ParticipantName,
        booking.ParticipantEmail, booking.Status, booking.CreatedAt, booking.ConfirmedAt, booking.CancelledAt,
        booking.CourseId is null);
    protected static AdminBookingResponse AdminBooking(BookingView booking) => new(booking.Id.Value,
        booking.CourseId?.Value ?? Guid.Empty, booking.Workshop.Value, booking.PriceCents / 100m, booking.ParticipantName,
        booking.ParticipantEmail, booking.Status, booking.CreatedAt, booking.ConfirmedAt, booking.CancelledAt,
        booking.CourseId is null);
}
