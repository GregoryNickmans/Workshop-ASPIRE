using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Application.Models;

public sealed record BookingFilter(CourseId? CourseId, string? Status, string? Search, int Page, int PageSize);
