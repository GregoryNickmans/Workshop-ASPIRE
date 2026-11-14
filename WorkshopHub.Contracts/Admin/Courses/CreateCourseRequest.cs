namespace WorkshopHub.Contracts.Admin.Courses;

public sealed record CreateCourseRequest(string Title, string ShortDescription, decimal Price, int MaximumParticipants);
