namespace WorkshopHub.Contracts.Courses;

public sealed record CourseResponse(Guid Id, string Title, string ShortDescription, decimal Price,
    int MaximumParticipants, int RemainingSeats);
