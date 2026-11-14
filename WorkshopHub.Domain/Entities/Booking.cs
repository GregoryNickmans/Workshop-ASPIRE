using ErrorOr;
using WorkshopHub.Domain.Events;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Domain.Entities;

// Synthetic workshop data only. Notification bookkeeping is not a broker outbox.
public sealed class Booking
{
    private readonly List<BookingConfirmed> _domainEvents = [];

    public static ErrorOr<Booking> Create(string? workshop, TimeProvider timeProvider)
    {
        var title = WorkshopTitle.Create(workshop);
        if (title.IsError)
        {
            return title.Errors;
        }
        return new Booking { Id = new BookingId(Guid.NewGuid()), Workshop = title.Value, CreatedAt = timeProvider.GetUtcNow() };
    }

    public static ErrorOr<Booking> Create(Course course, string? participantName, string? participantEmail, TimeProvider timeProvider)
    {
        var name = participantName?.Trim();
        var email = participantEmail?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || name.Any(char.IsControl))
        {
            return Error.Validation("Booking.ParticipantName", "Participant name must contain 1–100 characters.");
        }
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || email.Any(char.IsWhiteSpace) ||
            !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email ||
            !email.Contains('@') || email.Any(char.IsControl))
        {
            return Error.Validation("Booking.ParticipantEmail", "Enter a valid email address, up to 254 characters.");
        }
        if (course.IsArchived) { return Error.Conflict("Course.Archived", "This course is archived."); }
        if (course.RemainingSeats <= 0) { return Error.Conflict("Course.Full", "This course has no remaining seats."); }
        return new Booking
        {
            Id = new BookingId(Guid.NewGuid()),
            CourseId = course.Id,
            Workshop = course.Title,
            PriceCents = course.PriceCents,
            ParticipantName = name,
            ParticipantEmail = email.ToLowerInvariant(),
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    private Booking() { }
    public CourseId? CourseId { get; private set; }
    public int? PriceCents { get; private set; }
    public string? ParticipantName { get; private set; }
    public string? ParticipantEmail { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public BookingId Id { get; private set; }
    public WorkshopTitle Workshop { get; private set; } = null!;
    public string Status { get; private set; } = "Pending";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? NotificationPublishedAt { get; private set; }
    public string? AttachmentBlobName { get; private set; }
    public string? AttachmentFileName { get; private set; }
    public string? AttachmentContentType { get; private set; }
    public long? AttachmentLength { get; private set; }
    public bool IsDeleted { get; private set; }
    public IReadOnlyList<BookingConfirmed> DomainEvents => _domainEvents.AsReadOnly();

    public ErrorOr<Success> Confirm(TimeProvider timeProvider)
    {
        if (IsDeleted)
        {
            return Error.Conflict("Booking.Deleted", "A deleted booking cannot be confirmed.");
        }
        if (Status == "Cancelled")
        {
            return Error.Conflict("Booking.Cancelled", "Cancelled bookings cannot be confirmed.");
        }
        if (Status == "Confirmed")
        {
            return Result.Success;
        }
        Status = "Confirmed";
        ConfirmedAt = timeProvider.GetUtcNow();
        _domainEvents.Add(new BookingConfirmed(Id, ConfirmedAt.Value));
        return Result.Success;
    }

    public ErrorOr<Success> Attach(AttachmentMetadata attachment)
    {
        if (IsDeleted)
        {
            return Error.Conflict("Booking.Deleted", "A deleted booking cannot receive an attachment.");
        }
        AttachmentBlobName = attachment.StorageKey;
        AttachmentFileName = attachment.FileName;
        AttachmentContentType = attachment.ContentType;
        AttachmentLength = attachment.Length;
        return Result.Success;
    }

    public ErrorOr<Success> MarkNotificationPublished(TimeProvider timeProvider)
    {
        if (Status != "Confirmed")
        {
            return Error.Conflict("Booking.NotConfirmed", "Only confirmed bookings can publish confirmation notifications.");
        }
        NotificationPublishedAt ??= timeProvider.GetUtcNow();
        return Result.Success;
    }

    public ErrorOr<Success> Cancel(TimeProvider timeProvider)
    {
        if (IsDeleted) { return Error.Conflict("Booking.Deleted", "A deleted booking cannot be cancelled."); }
        if (Status == "Cancelled") { return Result.Success; }
        Status = "Cancelled";
        CancelledAt = timeProvider.GetUtcNow();
        return Result.Success;
    }

    internal Booking Copy() => (Booking)MemberwiseClone();

    public ErrorOr<Success> Delete()
    {
        if (CourseId is not null)
        {
            return Error.Conflict("Booking.CourseLinked", "Course bookings must be cancelled through the inventory transaction, not deleted.");
        }
        IsDeleted = true;
        return Result.Success;
    }
}
