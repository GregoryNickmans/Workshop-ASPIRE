namespace WorkshopHub.Application.Models;

public sealed record BookingPage(IReadOnlyList<BookingView> Items, int TotalCount, int Page, int PageSize);
