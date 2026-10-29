using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OneCover.Disbursements.Api.Data;
using OneCover.Disbursements.Api.Models;
using OneCover.Disbursements.Api.Models.Enums;

namespace OneCover.Disbursements.Api.Repositories;

/// <summary>The EF Core implementation of IDisbursementRepository, talking to DisbursementsDb.</summary>
// ": IDisbursementRepository" promises this class provides every method in the interface.
public class DisbursementRepository : IDisbursementRepository
{
    // SQL Server error numbers for "duplicate value in a unique index" (2601) and
    // "duplicate value in a unique constraint" (2627). Either one means the row already exists.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    // readonly = can be set once (in the constructor) and never reassigned.
    private readonly DisbursementsDbContext _db;

    // Constructor injection: ASP.NET Core sees this needs a DisbursementsDbContext and supplies the one
    // we registered in Program.cs.
    public DisbursementRepository(DisbursementsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Disbursement?> GetByIdAsync(int id)
    {
        // FirstOrDefaultAsync(condition) = run the query, return the first match, or null if none.
        // (Tracking is on by default, so edits to the returned object are saved by SaveChangesAsync.)
        return await _db.Disbursements.FirstOrDefaultAsync(d => d.Id == id);
    }

    /// <inheritdoc />
    public async Task<Disbursement?> GetByIdForMemberAsync(int id, int memberId)
    {
        // Same as above, but the condition also requires the row to belong to this member.
        // If it belongs to someone else, nothing matches, so we return null.
        return await _db.Disbursements.FirstOrDefaultAsync(d => d.Id == id && d.MemberId == memberId);
    }

    /// <inheritdoc />
    public async Task<Disbursement?> GetByClaimIdAsync(int claimId)
    {
        return await _db.Disbursements
            .AsNoTracking()                            // read-only: we only check existence, so skip change tracking
            .FirstOrDefaultAsync(d => d.ClaimId == claimId);   // first match for this claim, or null
    }

    /// <inheritdoc />
    public async Task<List<Disbursement>> ListForMemberAsync(int memberId, int skip = 0, int take = 50)
    {
        return await _db.Disbursements
            .AsNoTracking()                       // read-only: skip change tracking, which is faster for lists we won't edit
            .Where(d => d.MemberId == memberId)   // keep only this member's rows
            .OrderByDescending(d => d.DueDate)    // sort so the latest due date comes first
            .Skip(skip)                           // jump over the rows belonging to earlier pages
            .Take(take)                           // then keep at most one page of rows
            .ToListAsync();                       // NOW run the SQL and return the rows as a List
        // Nothing hits the database until ToListAsync. Before that, we are only building up the query.
    }

    /// <inheritdoc />
    public async Task<List<Disbursement>> ListAllAsync(DisbursementStatus? status = null, int skip = 0, int take = 50)
    {
        // IQueryable = a query that is described but not yet run. We can keep adding to it.
        IQueryable<Disbursement> query = _db.Disbursements.AsNoTracking();

        // Only add the filter if the caller asked for one. Otherwise return every status.
        if (status is not null)
        {
            query = query.Where(d => d.Status == status);   // keep only rows with this status
        }

        return await query
            .OrderByDescending(d => d.DueDate)   // latest due date first
            .ThenByDescending(d => d.Id)         // tie-breaker so rows with the same date always page in a stable order
            .Skip(skip)                          // jump over earlier pages
            .Take(take)                          // keep at most one page
            .ToListAsync();                      // run the SQL and return a List
    }

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(Disbursement disbursement)
    {
        // Add (not AddAsync): it only marks the object as "new" in memory, no database call, so no await needed.
        _db.Disbursements.Add(disbursement);

        try
        {
            // The INSERT happens here. If another handler already inserted this ClaimId, the
            // unique index makes SQL Server reject it and EF Core throws DbUpdateException.
            await _db.SaveChangesAsync();
            return true;
        }
        // "when (...)" = only catch this exception if the condition is true; any other DbUpdateException still escapes.
        // InnerException is the underlying SqlException; its Number says which SQL error happened.
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql
            && (sql.Number == UniqueIndexViolation || sql.Number == UniqueConstraintViolation))
        {
            // The disbursement for this claim already exists, which is fine (idempotency).
            // Detach our failed object so it isn't retried by a later SaveChangesAsync on this context.
            _db.Entry(disbursement).State = EntityState.Detached;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync()
    {
        // Sends every pending update to SQL Server in a single transaction.
        // The RowVersion check makes this throw DbUpdateConcurrencyException if the row changed underneath us.
        await _db.SaveChangesAsync();
    }
}
