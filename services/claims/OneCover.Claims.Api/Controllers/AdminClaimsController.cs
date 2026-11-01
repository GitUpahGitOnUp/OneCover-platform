using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Models.Enums;
using OneCover.Claims.Api.Security;
using OneCover.Claims.Api.Services;

namespace OneCover.Claims.Api.Controllers;

/// <summary>Admin endpoints: review every claim and move it through the workflow.</summary>
[ApiController]
[Route("api/admin/claims")]
// Only the Admin role may call anything in this controller. A Member gets 403 Forbidden.
[Authorize(Roles = "Admin")]
public class AdminClaimsController : ControllerBase
{
    private readonly IAdminClaimService _service;

    public AdminClaimsController(IAdminClaimService service)
    {
        _service = service;
    }

    /// <summary>Lists all claims, optionally only those with one status, newest first.</summary>
    /// <param name="status">Optional filter: Submitted, InReview, InfoRequested, Approved, Denied or Closed. Leave out for all.</param>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Rows per page (maximum 100).</param>
    [HttpGet]
    public async Task<ActionResult<List<ClaimDto>>> List(
        [FromQuery] ClaimStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return Ok(await _service.ListAllAsync(status, page, pageSize));
    }

    /// <summary>Gets any claim by id. Returns 404 if it doesn't exist.</summary>
    /// <param name="id">The claim's id.</param>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClaimDto>> Get(int id)
    {
        return Ok(await _service.GetAsync(id));
    }

    /// <summary>Moves a claim to a new status. Approving it also tells Disbursements to create the payout.</summary>
    /// <param name="id">The claim's id.</param>
    /// <param name="request">The new status and an optional note.</param>
    // The admin's own id comes from the JWT (never the body), so the history records who really made the change.
    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<ClaimDto>> UpdateStatus(int id, UpdateClaimStatusRequest request)
    {
        return Ok(await _service.UpdateStatusAsync(id, User.GetUserId(), request));
    }
}
