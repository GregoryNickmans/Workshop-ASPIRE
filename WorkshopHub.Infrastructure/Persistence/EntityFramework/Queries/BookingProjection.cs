using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Infrastructure.Persistence.EntityFramework.Queries;

internal static class BookingProjection
{
    public static BookingView Map(Booking booking, TicketId? ticketId = null) => new(booking.Id, booking.Workshop,
        booking.Status, booking.CreatedAt, booking.ConfirmedAt, booking.AttachmentFileName, booking.AttachmentContentType,
        booking.AttachmentLength, booking.CourseId, booking.PriceCents, booking.ParticipantName, booking.ParticipantEmail,
        booking.CancelledAt, ticketId);
    public static CourseView Map(Course course) => new(course.Id, course.Title, course.ShortDescription, course.PriceCents,
        course.MaximumParticipants, course.OccupiedSeats, course.IsArchived, course.Revision);
}
