using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Application.Models;
using WorkshopHub.Application.Services;
using WorkshopHub.Contracts.Tickets;
using WorkshopHub.Domain.ValueObjects;
using TicketEntity = WorkshopHub.Domain.Entities.Ticket;

namespace WorkshopHub.Api.Controllers.Admin;

// Like the rest of the admin routes, "admin" is organizational, not authorization.
// These endpoints are intentionally available in every environment and are NOT authenticated:
// do not expose them to the public internet. Storage stays private and downloads are API-mediated.
[ApiController]
[Route("api/v1/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminTicketsController(TicketRequestService ticketRequests, TicketService ticketService) : WorkshopController
{
    // Requests generation instead of performing it. The answer is 202 when the work is queued and 200
    // when the ticket row already exists; a repeated request re-submits the same stable identity.
    [HttpPost("bookings/{bookingId:guid}/ticket")]
    public async Task<IActionResult> Queue(Guid bookingId, CancellationToken cancellationToken)
    {
        var result = await ticketRequests.RequestAsync(new BookingId(bookingId), cancellationToken);
        if (result.IsError) { return Failure(result.Errors); }

        var request = result.Value;
        Response.Headers.CacheControl = "no-store";
        return request.State == TicketRequestState.Ready
            ? Ok(TicketRequest(request))
            : Accepted(TicketRequest(request));
    }

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult> Get(Guid ticketId, CancellationToken cancellationToken)
    {
        var result = await ticketService.GetAsync(new TicketId(ticketId), cancellationToken);
        return result.IsError ? Failure(result.Errors) : Ok(Ticket(result.Value));
    }

    [HttpGet("tickets/{ticketId:guid}/pdf")]
    public async Task<IActionResult> Download(Guid ticketId, CancellationToken cancellationToken)
    {
        var metadata = await ticketService.GetAsync(new TicketId(ticketId), cancellationToken);
        if (metadata.IsError) { return Failure(metadata.Errors); }

        var result = await ticketService.OpenAsync(metadata.Value, cancellationToken);
        if (result.IsError) { return Failure(result.Errors); }

        Response.Headers.CacheControl = "no-store";
        // File(...) owns the stream and disposes it after the response is written.
        return File(result.Value.Content, TicketEntity.PdfContentType, metadata.Value.FileName);
    }
}
