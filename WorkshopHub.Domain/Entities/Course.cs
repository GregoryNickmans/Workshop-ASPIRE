using ErrorOr;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Domain.Entities;

public sealed class Course
{
    private Course() { }

    public CourseId Id { get; private set; }
    public WorkshopTitle Title { get; private set; } = null!;
    public string ShortDescription { get; private set; } = "";
    public int PriceCents { get; private set; }
    public decimal Price => PriceCents / 100m;
    public int MaximumParticipants { get; private set; }
    public int OccupiedSeats { get; private set; }
    public bool IsArchived { get; private set; }
    public int Revision { get; private set; }
    public int RemainingSeats => MaximumParticipants - OccupiedSeats;

    public static ErrorOr<Course> Create(string? title, string? shortDescription, decimal price, int maximumParticipants)
    {
        var course = new Course { Id = new CourseId(Guid.NewGuid()) };
        var result = course.SetDetails(title, shortDescription, price, maximumParticipants);
        return result.IsError ? result.Errors : course;
    }

    public ErrorOr<Success> Update(string? title, string? shortDescription, decimal price, int maximumParticipants, int revision)
    {
        if (revision != Revision)
        {
            return Error.Conflict("Course.Revision", "This course changed. Reload it before editing.");
        }
        if (IsArchived)
        {
            return Error.Conflict("Course.Archived", "Archived courses cannot be edited.");
        }
        var result = SetDetails(title, shortDescription, price, maximumParticipants);
        if (!result.IsError) { Revision++; }
        return result;
    }

    public ErrorOr<Success> Archive(int revision)
    {
        if (IsArchived) { return Result.Success; }
        if (revision != Revision)
        {
            return Error.Conflict("Course.Revision", "This course changed. Reload it before archiving.");
        }
        IsArchived = true;
        Revision++;
        return Result.Success;
    }

    private ErrorOr<Success> SetDetails(string? title, string? shortDescription, decimal price, int maximumParticipants)
    {
        var validatedTitle = WorkshopTitle.Create(title);
        if (validatedTitle.IsError) { return validatedTitle.Errors; }
        var description = shortDescription?.Trim();
        if (string.IsNullOrWhiteSpace(description) || description.Length > 300)
        {
            return Error.Validation("Course.ShortDescription", "Description must contain 1–300 characters.");
        }
        if (price < 0 || price > int.MaxValue / 100m || decimal.Round(price, 2) != price)
        {
            return Error.Validation("Course.Price", "Price must be nonnegative, at most 21,474,836.47, with at most two decimal places.");
        }
        if (maximumParticipants <= 0)
        {
            return Error.Validation("Course.MaximumParticipants", "Capacity must be a positive whole number.");
        }
        if (maximumParticipants < OccupiedSeats)
        {
            return Error.Conflict("Course.Capacity", "Capacity cannot be reduced below occupied seats.");
        }
        Title = validatedTitle.Value;
        ShortDescription = description;
        PriceCents = (int)(price * 100m);
        MaximumParticipants = maximumParticipants;
        return Result.Success;
    }
}
