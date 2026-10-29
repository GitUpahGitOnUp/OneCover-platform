using Microsoft.EntityFrameworkCore;
using OneCover.Disbursements.Api.Models;

namespace OneCover.Disbursements.Api.Data;

/// <summary>
/// EF Core's gateway to the DisbursementsDb database. Owns the Disbursements table and
/// describes how each C# property maps to a column. No other service touches this database.
/// </summary>
// ": DbContext" inherits EF Core's base class, which provides all the query and save machinery.
public class DisbursementsDbContext : DbContext
{
    // Constructor. ASP.NET Core passes in the settings (which database, which provider) when it
    // creates this class. ": base(options)" hands those settings up to DbContext.
    public DisbursementsDbContext(DbContextOptions<DisbursementsDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// The Disbursements table. Query it like a list (db.Disbursements.Where(...)) and EF Core
    /// translates that into SQL.
    /// </summary>
    // Set<Disbursement>() gives EF Core a table for the Disbursement class.
    // "=> Set<...>()" is a read-only property that fetches it on demand.
    public DbSet<Disbursement> Disbursements => Set<Disbursement>();

    /// <summary>
    /// Runs once when EF Core builds its model of the database. This is where we set column
    /// types, indexes and other details that can't be guessed from the C# alone.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // modelBuilder.Entity<Disbursement>() = "I'm now configuring the Disbursement table".
        modelBuilder.Entity<Disbursement>(entity =>
        {
            // Money needs exact decimals: decimal(18,2) = up to 18 digits, 2 after the point.
            // Without this, EF Core warns and may pick a default that rounds.
            entity.Property(d => d.Amount).HasPrecision(18, 2);

            // Store the status as readable text ("Pending") instead of a number (0).
            // Easier to read in SSMS, and reordering the enum later can't corrupt old rows.
            entity.Property(d => d.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            // Cap the failure reason's length so the column is not nvarchar(max).
            entity.Property(d => d.FailureReason).HasMaxLength(500);

            // UNIQUE index on ClaimId: the database itself refuses a second disbursement for the
            // same claim. This is what makes the ClaimApproved handler idempotent (rule 5):
            // if the same event arrives twice, the second insert fails instead of paying twice.
            entity.HasIndex(d => d.ClaimId).IsUnique();

            // Plain index on MemberId: members fetch "my disbursements" often, so this speeds it up.
            entity.HasIndex(d => d.MemberId);
        });
    }
}
