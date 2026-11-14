namespace WorkshopHub.Contracts.Admin.Courses;

public sealed record AdminCourseResponse(Guid Id, string Title, string ShortDescription, decimal Price,
    int MaximumParticipants, int OccupiedSeats, bool IsArchived, int Revision);
