using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Services;

/// <summary>The admin use cases of the Claims service: review every claim and move it through the workflow.</summary>
public interface IAdminClaimService
{
    /// <summary>Lists all claims, optionally only those with one status, newest first.</summary>
    /// <param name="status">Only claims with this status, or null for all.</param>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Rows per page.</param>
    /// <param name="awaitingPayoutOnly">True to list only approved claims Disbursements has not confirmed (the dashboard warning).</param>
    Task<List<ClaimDto>> ListAllAsync(ClaimStatus? status, int page, int pageSize, bool awaitingPayoutOnly = false);

    /// <summary>Counts per status plus how many approved claims are still awaiting a payout, for the admin dashboard.</summary>
    Task<ClaimSummaryDto> GetSummaryAsync();

    /// <summary>
    /// Sends ClaimApproved again for an approved claim whose payout Disbursements never confirmed. Safe to repeat:
    /// Disbursements ignores a duplicate. Throws NotFoundException if the claim doesn't exist, BusinessRuleException if it
    /// was never approved or is already confirmed, and ServiceUnavailableException if Disbursements can't be reached.
    /// </summary>
    /// <param name="id">The claim to re-send.</param>
    Task<ClaimDto> ResendPayoutAsync(int id);

    /// <summary>Gets any claim. Throws NotFoundException if it doesn't exist.</summary>
    Task<ClaimDto> GetAsync(int id);

    /// <summary>
    /// Moves a claim to a new status and records who did it. Approving also sends ClaimApproved to Disbursements.
    /// Throws NotFoundException if the claim doesn't exist, and BusinessRuleException if the workflow doesn't allow the
    /// move or someone else changed the claim at the same moment.
    /// </summary>
    /// <param name="id">The claim to move.</param>
    /// <param name="adminId">The admin making the change (from the JWT, never the request).</param>
    /// <param name="request">The new status and an optional note.</param>
    Task<ClaimDto> UpdateStatusAsync(int id, int adminId, UpdateClaimStatusRequest request);
}
