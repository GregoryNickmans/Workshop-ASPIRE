using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WorkshopHub.Contracts.Admin.Bookings;
using WorkshopHub.Contracts.Admin.Courses;
using WorkshopHub.Contracts.Bookings;
using WorkshopHub.Contracts.Tickets;
using Xunit;
using Xunit.Abstractions;

namespace WorkshopHub.IntegrationTests;

[Collection(MessagingCollection.Name)]
public sealed class TicketMessagingTests(MessagingAppFixture fixture, ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConfirmBooking_GeneratesDownloadableTicket_AndRepeatRequestReturnsSameTicket()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var cancellationToken = budget.Token;
        var elapsed = Stopwatch.StartNew();
        var phase = "create course";
        try
        {
            var unique = Guid.NewGuid().ToString("N");
            var course = await CreateCourseAsync(unique, cancellationToken);

            phase = "create booking";
            var booking = await CreateBookingAsync(course.Id, unique, cancellationToken);
            output.WriteLine($"Course {course.Id}, booking {booking.Id}");

            phase = "confirm booking and publish command";
            var (ticketId, confirmedAt) = await ConfirmBookingAsync(booking.Id, cancellationToken);
            output.WriteLine($"Ticket {ticketId} queued after {elapsed.Elapsed}.");

            phase = "worker generation and completion-event persistence";
            var ticket = await WaitForGeneratedTicketAsync(ticketId, booking.Id, course.Id, confirmedAt, cancellationToken);

            phase = "download generated PDF";
            var pdf = await DownloadPdfAsync(ticket, cancellationToken);

            phase = "verify persisted booking";
            var persistedBooking = await VerifyPersistedBookingAsync(booking.Id, ticket.Id, confirmedAt, cancellationToken);

            phase = "repeat ready ticket request";
            await VerifyRepeatTicketRequestAsync(ticket, pdf, persistedBooking, cancellationToken);
            output.WriteLine($"Messaging flow and repeat request verified in {elapsed.Elapsed}.");
        }
        catch
        {
            output.WriteLine($"Failed during {phase} after {elapsed.Elapsed}.");
            output.WriteLine(await fixture.GetDiagnosticsAsync());
            throw;
        }
    }

    private async Task<AdminCourseResponse> CreateCourseAsync(string unique, CancellationToken cancellationToken)
    {
        using var response = await fixture.Api.PostAsJsonAsync("/api/v1/admin/courses",
            new CreateCourseRequest($"Messaging {unique}", "Integration test course", 49.95m, 5), cancellationToken);
        return await ReadResponseAsync<AdminCourseResponse>(response, HttpStatusCode.Created, cancellationToken);
    }

    private async Task<BookingResponse> CreateBookingAsync(Guid courseId, string unique, CancellationToken cancellationToken)
    {
        using var response = await fixture.Api.PostAsJsonAsync("/api/v1/bookings",
            new CreateBookingRequest(courseId, "Test Attendee", $"ticket-{unique}@example.invalid"), cancellationToken);
        var booking = await ReadResponseAsync<BookingResponse>(response, HttpStatusCode.Created, cancellationToken);
        Assert.Equal("Pending", booking.Status);
        return booking;
    }

    private async Task<(Guid TicketId, DateTimeOffset ConfirmedAt)> ConfirmBookingAsync(Guid bookingId,
        CancellationToken cancellationToken)
    {
        using var response = await fixture.Api.PostAsync($"/api/v1/admin/bookings/{bookingId}/confirm", null, cancellationToken);
        var confirmation = await ReadResponseAsync<AdminBookingConfirmationResponse>(response, HttpStatusCode.OK, cancellationToken);
        Assert.Equal(bookingId, confirmation.Booking.Id);
        Assert.Equal("Confirmed", confirmation.Booking.Status);
        Assert.NotNull(confirmation.Booking.ConfirmedAt);
        Assert.Equal(TicketRequestStatus.Queued, confirmation.TicketStatus);
        Assert.Equal("Booking confirmed. Its ticket is being generated; refresh in a moment to download it.",
            confirmation.TicketWarning);
        Assert.NotNull(confirmation.TicketId);
        Assert.NotEqual(Guid.Empty, confirmation.TicketId.Value);
        return (confirmation.TicketId.Value, confirmation.Booking.ConfirmedAt.Value);
    }

    private async Task<TicketResponse> WaitForGeneratedTicketAsync(Guid ticketId, Guid bookingId, Guid courseId,
        DateTimeOffset confirmedAt, CancellationToken cancellationToken)
    {
        var ticket = await WaitForTicketAsync(ticketId, cancellationToken);
        Assert.Equal(ticketId, ticket.Id);
        Assert.Equal(bookingId, ticket.BookingId);
        Assert.Equal(courseId, ticket.CourseId);
        Assert.Equal("application/pdf", ticket.ContentType);
        Assert.True(ticket.Length > 0);
        Assert.Equal(confirmedAt, ticket.CreatedAt);
        return ticket;
    }

    private async Task<AdminBookingResponse> VerifyPersistedBookingAsync(Guid bookingId, Guid ticketId,
        DateTimeOffset confirmedAt, CancellationToken cancellationToken)
    {
        var booking = await GetBookingAsync(bookingId, cancellationToken);
        Assert.Equal("Confirmed", booking.Status);
        Assert.Equal(ticketId, booking.TicketId);
        Assert.Equal(confirmedAt, booking.ConfirmedAt);
        return booking;
    }

    private async Task VerifyRepeatTicketRequestAsync(TicketResponse ticket, byte[] pdf,
        AdminBookingResponse persistedBooking, CancellationToken cancellationToken)
    {
        using var response = await fixture.Api.PostAsync($"/api/v1/admin/bookings/{persistedBooking.Id}/ticket", null, cancellationToken);
        var repeated = await ReadResponseAsync<TicketRequestResponse>(response, HttpStatusCode.OK, cancellationToken);
        Assert.Equal(TicketRequestStatus.Ready, repeated.Status);
        Assert.Equal(ticket.Id, repeated.TicketId);
        Assert.Equal(ticket, repeated.Ticket);
        using var metadataResponse = await fixture.Api.GetAsync($"/api/v1/admin/tickets/{ticket.Id}", cancellationToken);
        Assert.Equal(ticket, await ReadResponseAsync<TicketResponse>(metadataResponse, HttpStatusCode.OK, cancellationToken));
        Assert.Equal(pdf, await DownloadPdfAsync(ticket, cancellationToken));
        var afterRepeat = await GetBookingAsync(persistedBooking.Id, cancellationToken);
        Assert.Equal(persistedBooking, afterRepeat);
    }

    private async Task<TicketResponse> WaitForTicketAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        string lastResponse = "No response received.";
        try
        {
            while (true)
            {
                using var response = await fixture.Api.GetAsync($"/api/v1/admin/tickets/{ticketId}", cancellationToken);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return await ReadResponseAsync<TicketResponse>(response, HttpStatusCode.OK, cancellationToken);
                }
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                lastResponse = $"{(int)response.StatusCode}: {body}";
                Assert.True(response.StatusCode == HttpStatusCode.NotFound,
                    $"Unexpected response while awaiting ticket {ticketId}: {lastResponse}");
                using var problem = JsonDocument.Parse(body);
                Assert.Equal("Ticket.NotFound", problem.RootElement.GetProperty("title").GetString());
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Ticket {ticketId} was not persisted before the processing deadline. Last response: {lastResponse}");
        }
    }

    private async Task<AdminBookingResponse> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        using var response = await fixture.Api.GetAsync($"/api/v1/admin/bookings/{bookingId}", cancellationToken);
        return await ReadResponseAsync<AdminBookingResponse>(response, HttpStatusCode.OK, cancellationToken);
    }

    private async Task<byte[]> DownloadPdfAsync(TicketResponse ticket, CancellationToken cancellationToken)
    {
        using var response = await fixture.Api.GetAsync($"/api/v1/admin/tickets/{ticket.Id}/pdf", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        Assert.Equal(ticket.Length, bytes.LongLength);
        Assert.True(bytes.AsSpan().StartsWith("%PDF"u8), "Downloaded ticket must be a PDF.");
        return bytes;
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, HttpStatusCode expected,
        CancellationToken cancellationToken) where T : class
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == expected,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath}: expected {(int)expected}, got {(int)response.StatusCode}. {body}");
        var value = JsonSerializer.Deserialize<T>(body, JsonOptions);
        Assert.NotNull(value);
        return value;
    }
}
