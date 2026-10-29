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
    /// Finds the disbursement created for a claim, or null if none yet. Read-only: the returned
    /// object is not tracked, so changes to it are NOT saved. Use it to check "does one exist?".
    /// </summary>
    Task<Disbursement?> GetByClaimIdAsync(int claimId);

    /// <summary>Lists one member's disbursements, newest due date first, one page at a time.</summary>
    /// <param name="memberId">The member whose disbursements to list.</param>
    /// <param name="skip">How many rows to skip (page number minus 1, times page size).</param>
    /// <param name="take">The most rows to return (the page size).</param>
    // Paging = returning a slice of the results instead of the whole table, so the list stays fast as it grows.
    Task<List<Disbursement>> ListForMemberAsync(int memberId, int skip = 0, int take = 50);

    /// <summary>Lists all disbursements for admins, optionally only those with the given status, one page at a time.</summary>
    // "DisbursementStatus? status = null" is an OPTIONAL parameter: callers may leave it out.
    Task<List<Disbursement>> ListAllAsync(DisbursementStatus? status = null, int skip = 0, int take = 50);

    /// <summary>
    /// Inserts a new disbursement and saves it right away.
    /// Returns true if it was saved, or false if a disbursement for the same claim already exists
    /// (the same ClaimApproved event arrived twice). Either way the end result is exactly one row.
    /// </summary>
    Task<bool> TryAddAsync(Disbursement disbursement);

    /// <summary>
    /// Writes all staged changes (such as MarkPaid / MarkFailed edits) to the database in one transaction.
    /// Throws DbUpdateConcurrencyException if someone else changed the row since it was loaded.
    /// </summary>
    Task SaveChangesAsync();
}
