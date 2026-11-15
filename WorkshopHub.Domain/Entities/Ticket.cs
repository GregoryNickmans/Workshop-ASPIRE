using ErrorOr;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Domain.Entities;

// Attendee ticket metadata. PDF bytes live in private blob storage; SQL never stores them.
public sealed class Ticket
{
    public const long MaximumLength = 10 * 1024 * 1024;
    public const string PdfContentType = "application/pdf";

    private Ticket() { }

    public TicketId Id { get; private set; }
    public BookingId BookingId { get; private set; }
    public CourseId CourseId { get; private set; }
    public string StorageKey { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = PdfContentType;
    public long Length { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ErrorOr<Ticket> Create(BookingId bookingId, CourseId courseId, long length, TimeProvider timeProvider) =>
        Create(bookingId, courseId, new TicketId(Guid.NewGuid()), length, timeProvider);

    // The identity is passed in so a caller can render the PDF with the final ticket reference.
    // Storage key and filename are still derived here, never supplied, so no caller can choose a path.
    public static ErrorOr<Ticket> Create(BookingId bookingId, CourseId courseId, TicketId ticketId, long length,
        TimeProvider timeProvider)
    {
        if (bookingId.Value == Guid.Empty) { return Error.Validation("Ticket.Booking", "A ticket requires a booking."); }
        if (courseId.Value == Guid.Empty) { return Error.Validation("Ticket.Course", "A ticket requires a course."); }
        if (ticketId.Value == Guid.Empty) { return Error.Validation("Ticket.Id", "A ticket reference is required."); }
        if (length is <= 0 or > MaximumLength)
        {
            return Error.Validation("Ticket.Length", "A generated ticket must be between 1 byte and 10 MiB.");
        }

        return new Ticket
        {
            Id = ticketId,
            BookingId = bookingId,
            CourseId = courseId,
            StorageKey = StorageKeyFor(courseId, ticketId),
            FileName = FileNameFor(ticketId),
            Length = length,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    // Blob folders are virtual prefixes; the course segment keeps private storage browsable by course.
    public static string StorageKeyFor(CourseId courseId, TicketId ticketId) =>
        $"{courseId.Value:D}/{ticketId.Value:D}.pdf";

    public static string FileNameFor(TicketId ticketId) => $"workshophub-ticket-{ticketId.Value:N}.pdf";
}
