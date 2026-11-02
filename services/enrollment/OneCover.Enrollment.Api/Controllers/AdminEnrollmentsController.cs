using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCover.Enrollment.Api.Dtos;
using OneCover.Enrollment.Api.Services;

namespace OneCover.Enrollment.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/enrollments")]
public sealed class AdminEnrollmentsController(IEnrollmentService enrollmentService) : ControllerBase
{
    [HttpGet("summary")]
    [EndpointSummary("Get enrollment totals for the admin dashboard")]
    [ProducesResponseType<EnrollmentSummaryResponse>(StatusCodes.Status200OK)]
    public Task<EnrollmentSummaryResponse> GetSummary(CancellationToken cancellationToken) =>
        enrollmentService.GetSummaryAsync(cancellationToken);
}
