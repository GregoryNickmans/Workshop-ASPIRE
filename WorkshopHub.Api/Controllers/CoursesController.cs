using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Application.Services;
using WorkshopHub.Domain.ValueObjects;

namespace WorkshopHub.Api.Controllers;

[ApiController]
[Route("api/v1/courses")]
public sealed class CoursesController(CourseService courseService) : WorkshopController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok((await courseService.ListAsync(false, cancellationToken)).Select(PublicCourse));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await courseService.GetAsync(new CourseId(id), false, cancellationToken);
        return result.IsError ? Failure(result.Errors) : Ok(PublicCourse(result.Value));
    }
}
