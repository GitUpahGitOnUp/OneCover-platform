using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Security;
using OneCover.Claims.Api.Services;

namespace OneCover.Claims.Api.Controllers;

/// <summary>Member endpoints: a signed-in member files claims and sees only their own.</summary>
// [ApiController] turns on API conveniences, such as automatic 400 responses for bad request bodies.
[ApiController]
// The URL prefix for every action in this class: /api/claims
[Route("api/claims")]
// Only requests with a valid JWT whose role is Member may use this controller. An Admin gets 403.
[Authorize(Roles = "Member")]
public class ClaimsController : ControllerBase
{
    private readonly IClaimService _service;

    // DI supplies the service when ASP.NET Core creates the controller for each request.
    public ClaimsController(IClaimService service)
    {
        _service = service;
    }

    /// <summary>Lists the signed-in member's claims, newest first.</summary>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Rows per page (maximum 100).</param>
    [HttpGet]
    public async Task<ActionResult<List<ClaimDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        // User = the signed-in user from the JWT. The member id NEVER comes from the URL or body.
        return Ok(await _service.ListForMemberAsync(User.GetUserId(), page, pageSize));
    }

    /// <summary>Gets one of the signed-in member's claims. Returns 404 if it isn't theirs.</summary>
    /// <param name="id">The claim's id.</param>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClaimDto>> Get(int id)
    {
        return Ok(await _service.GetForMemberAsync(id, User.GetUserId()));
    }

    /// <summary>Files a new claim. The benefit amount is calculated by the server, never sent by the client.</summary>
    /// <param name="request">The enrollment, reason and leave dates.</param>
    // CreatedAtAction = HTTP 201 Created, plus a Location header pointing at GET /api/claims/{id} for the new claim.
    [HttpPost]
    public async Task<ActionResult<ClaimDto>> Submit(
        SubmitClaimRequest request,
        CancellationToken cancellationToken)
    {
        var claim = await _service.SubmitAsync(User.GetUserId(), request, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = claim.Id }, claim);
    }
}
