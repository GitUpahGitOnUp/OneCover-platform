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
    /// <param name="awaitingPayout">Set to true to list only approved claims whose payout Disbursements has not confirmed.</param>
    [HttpGet]
    public async Task<ActionResult<List<ClaimDto>>> List(
        [FromQuery] ClaimStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool awaitingPayout = false)
    {
        return Ok(await _service.ListAllAsync(status, page, pageSize, awaitingPayout));
    }

    /// <summary>Counts per status plus the number of approved claims awaiting a payout, for the admin dashboard.</summary>
    // "summary" is a fixed word, and "{id:int}" below only matches numbers, so the two routes never clash.
    [HttpGet("summary")]
    public async Task<ActionResult<ClaimSummaryDto>> Summary()
    {
        return Ok(await _service.GetSummaryAsync());
    }

    /// <summary>
    /// Sends the payout request for an approved claim again, when Disbursements never confirmed it. Safe to click twice.
    /// Returns 409 if the claim was never approved or is already confirmed, and 503 if Disbursements is still unreachable.
    /// </summary>
    /// <param name="id">The claim's id.</param>
    [HttpPost("{id:int}/resend-payout")]
    public async Task<ActionResult<ClaimDto>> ResendPayout(int id)
    {
        return Ok(await _service.ResendPayoutAsync(id));
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
