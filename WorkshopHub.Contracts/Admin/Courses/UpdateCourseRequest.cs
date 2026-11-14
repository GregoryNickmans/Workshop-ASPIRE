namespace WorkshopHub.Contracts.Admin.Courses;

public sealed record UpdateCourseRequest(string Title, string ShortDescription, decimal Price, int MaximumParticipants, int Revision);
