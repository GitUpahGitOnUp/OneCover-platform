using Microsoft.AspNetCore.Mvc;
using OneCover.Contracts.Events;
using OneCover.Disbursements.Api.Security;
using OneCover.Disbursements.Api.Services;

namespace OneCover.Disbursements.Api.Controllers;

/// <summary>
/// Receives events from other services. Service-to-service only: the gateway does not route
/// /internal/** (CLAUDE.md rule 4), and every action here requires the shared internal key.
/// </summary>
// [ApiController] turns on API conveniences, such as automatic 400 errors for a bad request body.
// [Route] sets the URL prefix for every action in this class.
// [InternalApiKey] is the attribute from Step 2: callers without the right key get a 401.
[ApiController]
[Route("internal/events")]
[InternalApiKey]
public class InternalEventsController : ControllerBase
{
    private readonly IDisbursementService _service;

    // Constructor: ASP.NET's dependency injection supplies the service. The controller only
    // routes the request; the rules live in the service and entity (thin controller).
    public InternalEventsController(IDisbursementService service)
    {
        _service = service;
    }

    /// <summary>
    /// Claims calls this after approving a claim. Creates one Pending disbursement for it.
    /// Safe to call twice with the same event: the second call creates nothing and still succeeds.
    /// </summary>
    // POST internal/events/claim-approved. [FromBody] = read the JSON body into a ClaimApproved record.
    // async/await = the method pauses while the database works, without blocking a thread.
    [HttpPost("claim-approved")]
    public async Task<IActionResult> HandleClaimApprovedAsync([FromBody] ClaimApproved claimApproved)
    {
        // The service returns true for "created" and false for "already existed".
        // Both are success for the caller, so the response is 204 No Content either way.
        await _service.CreateForApprovedClaimAsync(
            claimId: claimApproved.ClaimId,
            memberId: claimApproved.MemberId,
            amount: claimApproved.Amount,
            approvedOn: claimApproved.ApprovedOn);

        return NoContent();
    }
}
