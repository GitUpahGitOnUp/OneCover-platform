using Microsoft.EntityFrameworkCore;
using OneCover.Disbursements.Api.Data;
using OneCover.Disbursements.Api.Models;
using OneCover.Disbursements.Api.Models.Enums;

namespace OneCover.Disbursements.Api.Repositories;

/// <summary>The EF Core implementation of IDisbursementRepository, talking to DisbursementsDb.</summary>
// ": IDisbursementRepository" promises this class provides every method in the interface.
public class DisbursementRepository : IDisbursementRepository
{
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
        return await _db.Disbursements.FirstOrDefaultAsync(d => d.ClaimId == claimId);
    }

    /// <inheritdoc />
    public async Task<List<Disbursement>> ListForMemberAsync(int memberId)
    {
        return await _db.Disbursements
            .AsNoTracking()                       // read-only: skip change tracking, which is faster for lists we won't edit
            .Where(d => d.MemberId == memberId)   // keep only this member's rows
            .OrderByDescending(d => d.DueDate)    // sort so the latest due date comes first
            .ToListAsync();                       // NOW run the SQL and return the rows as a List
        // Nothing hits the database until ToListAsync. Before that, we are only building up the query.
    }

    /// <inheritdoc />
    public async Task<List<Disbursement>> ListAllAsync(DisbursementStatus? status = null)
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
            .ToListAsync();                      // run the SQL and return a List
    }

    /// <inheritdoc />
    public async Task AddAsync(Disbursement disbursement)
    {
        // Marks the object as "new". Nothing is written until SaveChangesAsync.
        await _db.Disbursements.AddAsync(disbursement);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync()
    {
        // Sends every pending insert/update to SQL Server in a single transaction.
        // If the unique ClaimId index is violated, this is where the exception surfaces.
        await _db.SaveChangesAsync();
    }
}
