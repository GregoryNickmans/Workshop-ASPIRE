using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Application.Models;
using WorkshopHub.Contracts.Admin.Bookings;
using WorkshopHub.Contracts.Admin.Courses;
using WorkshopHub.Contracts.Bookings;
using WorkshopHub.Contracts.Common;
using WorkshopHub.Contracts.Courses;
using WorkshopHub.Contracts.Tickets;
using WorkshopHub.Domain.ValueObjects;

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
        booking.CourseId is null, booking.TicketId?.Value);
    // The confirmation reports the ticket identity and whether it is queued or ready, never a ready
    // download for work the worker has not finished.
    protected static AdminBookingConfirmationResponse Confirmation(BookingConfirmation confirmation) =>
        new(AdminBooking(confirmation.Booking), confirmation.TicketId?.Value,
            confirmation.TicketState?.ToString(), confirmation.TicketWarning);
    protected static TicketResponse Ticket(TicketView ticket) => new(ticket.Id.Value, ticket.BookingId.Value,
        ticket.CourseId.Value, ticket.FileName, ticket.ContentType, ticket.Length, ticket.CreatedAt);
    protected static TicketRequestResponse TicketRequest(TicketRequest request) => request.State == TicketRequestState.Ready
        ? Contracts.Tickets.TicketRequestResponse.Ready(Ticket(request.Ticket!))
        : Contracts.Tickets.TicketRequestResponse.Queued(request.TicketId.Value);
}
