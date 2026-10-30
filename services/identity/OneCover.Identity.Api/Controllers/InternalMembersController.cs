using Microsoft.AspNetCore.Mvc;
using OneCover.Identity.Api.Dtos;
using OneCover.Identity.Api.Security;
using OneCover.Identity.Api.Services;

namespace OneCover.Identity.Api.Controllers;

[ApiController]
[InternalApiKey]
[Route("internal/members")]
public sealed class InternalMembersController(IProfileService profileService) : ControllerBase
{
    [HttpGet("{id:int}")]
    [EndpointSummary("Get member facts for an internal service")]
    [ProducesResponseType<InternalMemberResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<InternalMemberResponse> Get(int id, CancellationToken cancellationToken) =>
        profileService.GetInternalMemberAsync(id, cancellationToken);
}
