using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCover.Enrollment.Api.Dtos;
using OneCover.Enrollment.Api.Services;

namespace OneCover.Enrollment.Api.Controllers;

[ApiController]
[Route("api/plans")]
public sealed class PlansController(IPlanService planService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List active plans and their available rates")]
    [ProducesResponseType<IReadOnlyList<PlanResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PlanResponse>> GetActive(CancellationToken cancellationToken) =>
        planService.GetActiveAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [EndpointSummary("Get an active plan and its rates")]
    [ProducesResponseType<PlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PlanResponse> GetById(int id, CancellationToken cancellationToken) =>
        planService.GetActiveByIdAsync(id, cancellationToken);

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [EndpointSummary("Create a plan and its full set of tier rates")]
    [ProducesResponseType<PlanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanResponse>> Create(
        SavePlanRequest request,
        CancellationToken cancellationToken)
    {
        var plan = await planService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = plan.Id }, plan);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [EndpointSummary("Replace an active plan and its tier rates")]
    [ProducesResponseType<PlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PlanResponse> Update(
        int id,
        SavePlanRequest request,
        CancellationToken cancellationToken) =>
        planService.UpdateAsync(id, request, cancellationToken);

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    [EndpointSummary("Retire a plan without deleting enrollment history")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retire(int id, CancellationToken cancellationToken)
    {
        await planService.RetireAsync(id, cancellationToken);
        return NoContent();
    }
}
