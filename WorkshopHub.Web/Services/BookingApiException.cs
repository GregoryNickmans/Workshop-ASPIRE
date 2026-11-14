using System.Net;

namespace WorkshopHub.Web.Services;

public sealed class BookingApiException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError,
    string? code = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? Code { get; } = code;
}
