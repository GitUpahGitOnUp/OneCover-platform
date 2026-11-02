using Microsoft.AspNetCore.Mvc;
using OneCover.Enrollment.Api.Dtos;
using OneCover.Enrollment.Api.Security;
using OneCover.Enrollment.Api.Services;

namespace OneCover.Enrollment.Api.Controllers;

[ApiController]
[InternalApiKey]
[Route("internal/enrollments")]
public sealed class InternalEnrollmentsController(IEnrollmentService enrollmentService) : ControllerBase
{
    [HttpGet("{id:int}")]
    [EndpointSummary("Get enrollment ownership and plan terms for an internal service")]
    [ProducesResponseType<InternalEnrollmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<InternalEnrollmentResponse> GetById(int id, CancellationToken cancellationToken) =>
        enrollmentService.GetInternalByIdAsync(id, cancellationToken);
}
