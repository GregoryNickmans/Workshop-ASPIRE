using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Models;

public sealed record BookingView(BookingId Id, WorkshopTitle Workshop, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt, string? AttachmentFileName, string? AttachmentContentType, long? AttachmentLength,
    CourseId? CourseId = null, int? PriceCents = null, string? ParticipantName = null,
    string? ParticipantEmail = null, DateTimeOffset? CancelledAt = null, TicketId? TicketId = null);
