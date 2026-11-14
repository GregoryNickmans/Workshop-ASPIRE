using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Models;

public sealed record CourseView(CourseId Id, WorkshopTitle Title, string ShortDescription, int PriceCents,
    int MaximumParticipants, int OccupiedSeats, bool IsArchived, int Revision);
