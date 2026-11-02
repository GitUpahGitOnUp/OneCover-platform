using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCover.Identity.Api.Dtos;
using OneCover.Identity.Api.Services;

namespace OneCover.Identity.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Get the authenticated user's profile and FMLA eligibility")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public Task<ProfileResponse> Get(CancellationToken cancellationToken) =>
        profileService.GetMineAsync(cancellationToken);

    [HttpPut]
    [EndpointSummary("Update the authenticated user's editable profile fields")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<ProfileResponse> Update(
        UpdateProfileRequest request,
        CancellationToken cancellationToken) =>
        profileService.UpdateMineAsync(request, cancellationToken);
}
