using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Repositories;

/// <summary>
/// Everything the service layer is allowed to ask of the BenefitClaims table.
/// An interface only declares the methods. The real code lives in ClaimRepository.
/// Every claim comes back with its History rows loaded, because the API always returns the full timeline.
/// </summary>
public interface IClaimRepository
{
    /// <summary>
    /// Finds a claim by id, or null if it doesn't exist. Used by admins. The returned claim is tracked, so
    /// changes made to it (such as MoveTo) are written by <see cref="SaveChangesAsync"/>.
    /// </summary>
    // Task<T> means "an async operation that will eventually produce a T". Callers use await.
    // "BenefitClaim?" means the result can be null (nothing found).
    Task<BenefitClaim?> GetByIdAsync(int id);

    /// <summary>
    /// Finds a claim by id, but only if it belongs to the given member. Someone else's claim returns null,
    /// so the API answers 404 and never confirms it exists. Read-only: changes to it are NOT saved.
    /// </summary>
    Task<BenefitClaim?> GetByIdForMemberAsync(int id, int memberId);

    /// <summary>Lists one member's claims, newest first, one page at a time.</summary>
    /// <param name="memberId">The member whose claims to list.</param>
    /// <param name="skip">How many rows to skip (page number minus 1, times page size).</param>
    /// <param name="take">The most rows to return (the page size).</param>
    // Paging = returning a slice of the results instead of the whole table, so the list stays fast as it grows.
    Task<List<BenefitClaim>> ListForMemberAsync(int memberId, int skip = 0, int take = 50);

    /// <summary>Lists all claims for admins, optionally only those with the given status, newest first, one page at a time.</summary>
    /// <param name="status">Only claims with this status, or null for every status.</param>
    /// <param name="skip">How many rows to skip.</param>
    /// <param name="take">The most rows to return.</param>
    /// <param name="awaitingPayoutOnly">True to list only approved claims Disbursements has not confirmed (the dashboard warning).</param>
    // "ClaimStatus? status = null" is an OPTIONAL parameter: callers may leave it out.
    Task<List<BenefitClaim>> ListAllAsync(ClaimStatus? status = null, int skip = 0, int take = 50, bool awaitingPayoutOnly = false);

    /// <summary>Counts the claims per status. A status with no claims has no entry, so the caller treats a missing one as zero.</summary>
    Task<Dictionary<ClaimStatus, int>> CountByStatusAsync();

    /// <summary>Counts the approved claims that Disbursements has not yet confirmed (the dashboard warning).</summary>
    Task<int> CountAwaitingPayoutAsync();

    /// <summary>Stages a new claim (and its first history row) and saves it right away.</summary>
    Task AddAsync(BenefitClaim claim);

    /// <summary>
    /// Writes all staged changes (such as a MoveTo edit) to the database in one transaction.
    /// Throws DbUpdateConcurrencyException if someone else changed the claim since it was loaded.
    /// </summary>
    Task SaveChangesAsync();
}
