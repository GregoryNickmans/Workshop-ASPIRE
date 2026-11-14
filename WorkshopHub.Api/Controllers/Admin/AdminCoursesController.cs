using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Application.Services;
using WorkshopHub.Contracts.Admin.Courses;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Api.Controllers.Admin;

// This namespace is organizational, NOT authorization. Unsafe for public deployment.
[ApiController]
[Route("api/v1/admin/courses")]
public sealed class AdminCoursesController(CourseService courseService) : WorkshopController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok((await courseService.ListAsync(true, cancellationToken)).Select(AdminCourse));
    [HttpPost]
    public async Task<IActionResult> Create(CreateCourseRequest request, CancellationToken cancellationToken)
    {
        var result = await courseService.CreateAsync(request.Title, request.ShortDescription, request.Price, request.MaximumParticipants, cancellationToken);
        return result.IsError ? Failure(result.Errors) : Created($"/api/v1/admin/courses/{result.Value.Id.Value}", AdminCourse(result.Value));
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        var result = await courseService.UpdateAsync(new CourseId(id), request.Title, request.ShortDescription, request.Price,
            request.MaximumParticipants, request.Revision, cancellationToken);
        return result.IsError ? Failure(result.Errors) : Ok(AdminCourse(result.Value));
    }
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, ArchiveCourseRequest request, CancellationToken cancellationToken)
    {
        var result = await courseService.ArchiveAsync(new CourseId(id), request.Revision, cancellationToken);
        return result.IsError ? Failure(result.Errors) : Ok(AdminCourse(result.Value));
    }
}
