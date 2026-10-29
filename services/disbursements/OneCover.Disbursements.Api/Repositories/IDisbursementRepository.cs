using OneCover.Disbursements.Api.Models;
using OneCover.Disbursements.Api.Models.Enums;

namespace OneCover.Disbursements.Api.Repositories;

/// <summary>
/// Everything the service layer is allowed to ask of the Disbursements table.
/// An interface only declares the methods. The real code lives in DisbursementRepository.
/// </summary>
public interface IDisbursementRepository
{
    /// <summary>Finds a disbursement by its id, or null if it doesn't exist. Used by admins.</summary>
    // Task<T> means "an async operation that will eventually produce a T". Callers use await.
    // "Disbursement?" means the result can be null (nothing found).
    Task<Disbursement?> GetByIdAsync(int id);

    /// <summary>
    /// Finds a disbursement by id, but only if it belongs to the given member.
    /// Someone else's record returns null, so the API answers 404 and never confirms it exists.
    /// </summary>
    Task<Disbursement?> GetByIdForMemberAsync(int id, int memberId);

    /// <summary>
    /// Finds the disbursement created for a claim, or null if none yet.
    /// The ClaimApproved handler uses this to stay idempotent (same event twice = one result).
    /// </summary>
    Task<Disbursement?> GetByClaimIdAsync(int claimId);

    /// <summary>Lists one member's disbursements, newest due date first.</summary>
    Task<List<Disbursement>> ListForMemberAsync(int memberId);

    /// <summary>Lists all disbursements for admins, optionally only those with the given status.</summary>
    // "DisbursementStatus? status = null" is an OPTIONAL parameter: callers may leave it out.
    Task<List<Disbursement>> ListAllAsync(DisbursementStatus? status = null);

    /// <summary>Stages a new disbursement to be inserted. Call SaveChangesAsync to write it.</summary>
    Task AddAsync(Disbursement disbursement);

    /// <summary>Writes all staged changes (inserts and edits) to the database in one transaction.</summary>
    Task SaveChangesAsync();
}
