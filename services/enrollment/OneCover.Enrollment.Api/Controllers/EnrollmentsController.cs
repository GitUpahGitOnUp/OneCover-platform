using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCover.Enrollment.Api.Dtos;
using OneCover.Enrollment.Api.Services;

namespace OneCover.Enrollment.Api.Controllers;

[ApiController]
[Authorize(Roles = "Member")]
[Route("api/enrollments")]
public sealed class EnrollmentsController(IEnrollmentService enrollmentService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List the authenticated member's enrollments")]
    [ProducesResponseType<IReadOnlyList<EnrollmentResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<EnrollmentResponse>> GetMine(CancellationToken cancellationToken) =>
        enrollmentService.GetMineAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [EndpointSummary("Get one of the authenticated member's enrollments")]
    [ProducesResponseType<EnrollmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<EnrollmentResponse> GetMineById(int id, CancellationToken cancellationToken) =>
        enrollmentService.GetMineByIdAsync(id, cancellationToken);

    [HttpPost]
    [EndpointSummary("Enroll in an active plan at an available coverage tier")]
    [ProducesResponseType<EnrollmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrollmentResponse>> Enroll(
        EnrollRequest request,
        CancellationToken cancellationToken)
    {
        var enrollment = await enrollmentService.EnrollAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMineById), new { id = enrollment.Id }, enrollment);
    }
}
