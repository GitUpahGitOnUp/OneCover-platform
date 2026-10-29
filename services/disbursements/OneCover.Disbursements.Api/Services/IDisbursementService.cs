using OneCover.Disbursements.Api.Dtos;
using OneCover.Disbursements.Api.Models.Enums;

namespace OneCover.Disbursements.Api.Services;

/// <summary>The use cases of the Disbursements service. Controllers and event handlers call this, never the repository.</summary>
public interface IDisbursementService
{
    /// <summary>
    /// Creates the Pending disbursement for an approved claim. Safe to call twice for the same claim:
    /// the second call creates nothing. Returns true if a new disbursement was created, false if one already existed.
    /// </summary>
    /// <param name="claimId">The approved claim.</param>
    /// <param name="memberId">The member to pay.</param>
    /// <param name="amount">The claim's total benefit.</param>
    /// <param name="approvedOn">The date the claim was approved. The due date is worked out from it.</param>
    Task<bool> CreateForApprovedClaimAsync(int claimId, int memberId, decimal amount, DateOnly approvedOn);

    /// <summary>Lists the signed-in member's own disbursements, newest due date first.</summary>
    Task<List<DisbursementDto>> ListForMemberAsync(int memberId, int page, int pageSize);

    /// <summary>Gets one of the member's own disbursements. Throws NotFoundException if it isn't theirs or doesn't exist.</summary>
    Task<DisbursementDto> GetForMemberAsync(int id, int memberId);

    /// <summary>Admin: lists all disbursements, optionally filtered by status.</summary>
    Task<List<DisbursementDto>> ListAllAsync(DisbursementStatus? status, int page, int pageSize);

    /// <summary>Admin: gets any disbursement. Throws NotFoundException if it doesn't exist.</summary>
    Task<DisbursementDto> GetAsync(int id);

    /// <summary>
    /// Admin: marks a Pending disbursement as Paid or Failed. Throws NotFoundException if it doesn't exist,
    /// and BusinessRuleException if the change breaks a rule or someone else changed it at the same moment.
    /// </summary>
    Task<DisbursementDto> UpdateStatusAsync(int id, UpdateDisbursementStatusRequest request);
}
