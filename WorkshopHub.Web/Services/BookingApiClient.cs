using System.Net.Http.Json;
using System.Text.Json;
using WorkshopHub.Contracts.Admin.Bookings;
using WorkshopHub.Contracts.Admin.Courses;
using WorkshopHub.Contracts.Bookings;
using WorkshopHub.Contracts.Common;
using WorkshopHub.Contracts.Courses;

namespace WorkshopHub.Web.Services;

// Every operation crosses HTTP. Mutations are never automatically retried.
public sealed class BookingApiClient(HttpClient httpClient)
{
    public Task<List<CourseResponse>> ListCoursesAsync(CancellationToken cancellationToken) =>
        GetAsync<List<CourseResponse>>("/api/v1/courses", cancellationToken);
    public Task<CourseResponse> GetCourseAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<CourseResponse>($"/api/v1/courses/{id}", cancellationToken);
    public Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken) =>
        SendAsync<BookingResponse>(HttpMethod.Post, "/api/v1/bookings", request, cancellationToken);
    public Task<List<AdminCourseResponse>> ListAdminCoursesAsync(CancellationToken cancellationToken) =>
        GetAsync<List<AdminCourseResponse>>("/api/v1/admin/courses", cancellationToken);
    public Task<AdminCourseResponse> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken) =>
        SendAsync<AdminCourseResponse>(HttpMethod.Post, "/api/v1/admin/courses", request, cancellationToken);
    public Task<AdminCourseResponse> UpdateCourseAsync(Guid id, UpdateCourseRequest request, CancellationToken cancellationToken) =>
        SendAsync<AdminCourseResponse>(HttpMethod.Put, $"/api/v1/admin/courses/{id}", request, cancellationToken);
    public Task<AdminCourseResponse> ArchiveCourseAsync(Guid id, int revision, CancellationToken cancellationToken) =>
        SendAsync<AdminCourseResponse>(HttpMethod.Post, $"/api/v1/admin/courses/{id}/archive", new ArchiveCourseRequest(revision), cancellationToken);
    public Task<PagedResponse<AdminBookingResponse>> ListBookingsAsync(Guid? courseId, string? status, string? search,
        int page, CancellationToken cancellationToken)
    {
        var query = $"page={page}&pageSize=20";
        if (courseId is not null) { query += $"&courseId={courseId}"; }
        if (!string.IsNullOrWhiteSpace(status)) { query += $"&status={Uri.EscapeDataString(status)}"; }
        if (!string.IsNullOrWhiteSpace(search)) { query += $"&search={Uri.EscapeDataString(search.Trim())}"; }
        return GetAsync<PagedResponse<AdminBookingResponse>>($"/api/v1/admin/bookings?{query}", cancellationToken);
    }
    public Task<AdminBookingResponse> GetBookingAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<AdminBookingResponse>($"/api/v1/admin/bookings/{id}", cancellationToken);
    public Task<AdminBookingResponse> ConfirmAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<AdminBookingResponse>(HttpMethod.Post, $"/api/v1/admin/bookings/{id}/confirm", null, cancellationToken);
    public Task<AdminBookingResponse> CancelAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<AdminBookingResponse>(HttpMethod.Post, $"/api/v1/admin/bookings/{id}/cancel", null, cancellationToken);

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }
    private async Task<T> SendAsync<T>(HttpMethod method, string uri, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body is not null) { request.Content = JsonContent.Create(body, body.GetType()); }
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new BookingApiException("The API returned an empty response. Refresh persisted state before retrying.", response.StatusCode);
        }
        var message = $"The API returned {(int)response.StatusCode}. Refresh persisted state before retrying.";
        string? code = null;
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (problem.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    code = title.GetString();
                }
                if (problem.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                {
                    message = detail.GetString() ?? message;
                }
                else if (code is not null) { message = code; }
            }
        }
        catch (JsonException) { /* Non-ProblemDetails errors retain a safe status-only message. */ }
        throw new BookingApiException(message, response.StatusCode, code);
    }
}
