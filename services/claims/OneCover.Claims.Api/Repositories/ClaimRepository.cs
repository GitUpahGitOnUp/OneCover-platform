using Microsoft.EntityFrameworkCore;
using OneCover.Claims.Api.Data;
using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Repositories;

/// <summary>The EF Core implementation of IClaimRepository, talking to ClaimsDb.</summary>
// ": IClaimRepository" promises this class provides every method in the interface.
public class ClaimRepository : IClaimRepository
{
    // readonly = can be set once (in the constructor) and never reassigned.
    private readonly ClaimsDbContext _db;

    // Constructor injection: ASP.NET Core sees this needs a ClaimsDbContext and supplies the one
    // we registered in Program.cs.
    public ClaimRepository(ClaimsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<BenefitClaim?> GetByIdAsync(int id)
    {
        return await _db.BenefitClaims
            .Include(c => c.History)                    // also load the claim's history rows (a JOIN), so the timeline is complete
            .FirstOrDefaultAsync(c => c.Id == id);      // run the query, return the first match, or null if none
        // Tracking is on by default, so edits to the returned claim are saved by SaveChangesAsync.
    }

    /// <inheritdoc />
    public async Task<BenefitClaim?> GetByIdForMemberAsync(int id, int memberId)
    {
        return await _db.BenefitClaims
            .AsNoTracking()                                          // read-only: members only view their claims, so skip change tracking
            .Include(c => c.History)                                 // also load the history rows
            .FirstOrDefaultAsync(c => c.Id == id && c.MemberId == memberId);   // the claim must ALSO belong to this member, else null
    }

    /// <inheritdoc />
    public async Task<List<BenefitClaim>> ListForMemberAsync(int memberId, int skip = 0, int take = 50)
    {
        return await _db.BenefitClaims
            .AsNoTracking()                          // read-only list: skip change tracking, which is faster
            .Include(c => c.History)                 // also load each claim's history rows
            .Where(c => c.MemberId == memberId)      // keep only this member's claims
            .OrderByDescending(c => c.SubmittedAt)   // newest first (the (MemberId, SubmittedAt) index serves this)
            .ThenByDescending(c => c.Id)             // tie-breaker so claims filed at the same moment always page in a stable order
            .Skip(skip)                              // jump over the rows belonging to earlier pages
            .Take(take)                              // then keep at most one page of rows
            .ToListAsync();                          // NOW run the SQL and return the claims as a List
        // Nothing hits the database until ToListAsync. Before that, we are only building up the query.
    }

    /// <inheritdoc />
    public async Task<List<BenefitClaim>> ListAllAsync(
        ClaimStatus? status = null, int skip = 0, int take = 50, bool awaitingPayoutOnly = false)
    {
        // IQueryable = a query that is described but not yet run. We can keep adding to it.
        IQueryable<BenefitClaim> query = _db.BenefitClaims
            .AsNoTracking()          // read-only list
            .Include(c => c.History);   // also load each claim's history rows

        // Only add the filter if the caller asked for one. Otherwise return every status.
        if (status is not null)
        {
            query = query.Where(c => c.Status == status);   // keep only claims with this status
        }

        // Only add the payout filter if asked: "approved at some point AND Disbursements has not confirmed".
        // Status alone can't find these, because an approved claim moves on to Closed, so we look for the Approved row
        // in its history: c.History.Any(...) = at least one history row matches (SQL: EXISTS).
        if (awaitingPayoutOnly)
        {
            query = query.Where(c => c.PayoutRequestedAt == null && c.History.Any(h => h.ToStatus == ClaimStatus.Approved));
        }

        return await query
            .OrderByDescending(c => c.SubmittedAt)   // newest first (the (Status, SubmittedAt) index serves the filtered case)
            .ThenByDescending(c => c.Id)             // stable paging tie-breaker
            .Skip(skip)                              // jump over earlier pages
            .Take(take)                              // keep at most one page
            .ToListAsync();                          // run the SQL and return a List
    }

    /// <inheritdoc />
    public async Task<Dictionary<ClaimStatus, int>> CountByStatusAsync()
    {
        // LINQ chain (runs as ONE SQL "GROUP BY" query):
        //   .GroupBy(c => c.Status)                    = put claims with the same status in one group
        //   .Select(g => new { g.Key, Count = ... })   = for each group, keep its status (Key) and how many are in it
        //   .ToDictionaryAsync(x => x.Key, x => x.Count) = run the query and return a lookup: status -> count
        return await _db.BenefitClaims
            .AsNoTracking()
            .GroupBy(c => c.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    /// <inheritdoc />
    public async Task<int> CountAwaitingPayoutAsync()
    {
        // CountAsync(condition) = run "SELECT COUNT(*) ... WHERE condition" and return just the number.
        // Same condition as the awaiting-payout list: approved at some point, and Disbursements has not confirmed.
        return await _db.BenefitClaims
            .CountAsync(c => c.PayoutRequestedAt == null && c.History.Any(h => h.ToStatus == ClaimStatus.Approved));
    }

    /// <inheritdoc />
    public async Task AddAsync(BenefitClaim claim)
    {
        // Add (not AddAsync): it only marks the claim, and its history rows, as "new" in memory. No database call yet.
        _db.BenefitClaims.Add(claim);

        // The INSERTs happen here: the claim first, then its history rows with the new ClaimId filled in.
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync()
    {
        // Sends every pending update to SQL Server in a single transaction.
        // The RowVersion check makes this throw DbUpdateConcurrencyException if the claim changed underneath us.
        await _db.SaveChangesAsync();
    }
}
