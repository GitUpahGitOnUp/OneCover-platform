using Microsoft.EntityFrameworkCore;
using OneCover.Claims.Api.Models;

namespace OneCover.Claims.Api.Data;

/// <summary>
/// EF Core's gateway to the ClaimsDb database. Owns the BenefitClaims and ClaimStatusHistories tables
/// and describes how each C# property maps to a column. No other service touches this database.
/// </summary>
// ": DbContext" inherits EF Core's base class, which provides all the query and save machinery.
public class ClaimsDbContext : DbContext
{
    // Constructor. ASP.NET Core passes in the settings (which database, which provider) when it creates
    // this class. ": base(options)" hands those settings up to DbContext.
    public ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : base(options)
    {
    }

    /// <summary>The BenefitClaims table. Query it like a list (db.BenefitClaims.Where(...)) and EF Core writes the SQL.</summary>
    // Set<BenefitClaim>() gives EF Core a table for the class. "=>" makes a read-only property fetch it on demand.
    public DbSet<BenefitClaim> BenefitClaims => Set<BenefitClaim>();

    /// <summary>The ClaimStatusHistories table: the audit trail of every status change.</summary>
    // The property is plural on purpose. A property named exactly "ClaimStatusHistory" would hide the class
    // of the same name inside this file (and the table would be named after the property).
    public DbSet<ClaimStatusHistory> ClaimStatusHistories => Set<ClaimStatusHistory>();

    /// <summary>
    /// Runs once when EF Core builds its model of the database. This is where we set column types,
    /// relationships and indexes that can't be guessed from the C# alone.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---- BenefitClaims table ----
        modelBuilder.Entity<BenefitClaim>(entity =>
        {
            // The text columns need a length. Without one, SQL Server would use nvarchar(max), which can't be indexed.
            entity.Property(c => c.Reason).HasMaxLength(BenefitClaim.MaxReasonLength);

            // Store the status as readable text ("InReview") instead of a number (1).
            // Easier to read in SSMS, and reordering the enum later can't corrupt old rows.
            entity.Property(c => c.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            // Money needs exact decimals: decimal(18,2) = up to 18 digits, 2 after the point.
            entity.Property(c => c.WeeklyBenefit).HasPrecision(18, 2);
            entity.Property(c => c.TotalBenefit).HasPrecision(18, 2);
            entity.Property(c => c.PlanWeeklyMaximum).HasPrecision(18, 2);

            // The benefit percent is a fraction like 0.6000, so it needs 4 decimal places but few digits overall.
            entity.Property(c => c.PlanBenefitPercent).HasPrecision(5, 4);

            // SQL Server "rowversion": the database bumps it on every update and EF Core checks it on save,
            // so two admins changing the same claim at once can't silently overwrite each other.
            entity.Property(c => c.RowVersion).IsRowVersion();

            // The History property has no setter; the real list is the private field _history.
            // PropertyAccessMode.Field tells EF Core to fill and read that field directly instead of the property.
            entity.Navigation(c => c.History).UsePropertyAccessMode(PropertyAccessMode.Field);

            // One claim has many history rows; each row points back with ClaimId.
            // Deleting a claim deletes its history too (cascade), since the trail means nothing without the claim.
            entity.HasMany(c => c.History)
                  .WithOne()
                  .HasForeignKey(h => h.ClaimId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Composite index on (MemberId, SubmittedAt): "my claims, newest first" is answered straight
            // from the index. It also covers queries on MemberId alone.
            entity.HasIndex(c => new { c.MemberId, c.SubmittedAt });

            // Composite index on (Status, SubmittedAt) for the admin list: "all InReview claims, newest first".
            // Status alone has only six values, so on its own it would rarely help. Paired with the date,
            // the database can filter on Status and read the rows already sorted, with no extra sort step.
            // There is deliberately no index on EnrollmentId yet: nothing queries by it. Add one in a later
            // migration if a real query needs it, because every index slows down inserts and updates a little.
            entity.HasIndex(c => new { c.Status, c.SubmittedAt });
        });

        // ---- ClaimStatusHistories table ----
        modelBuilder.Entity<ClaimStatusHistory>(entity =>
        {
            // Both status columns are stored as readable text, like BenefitClaims.Status.
            // FromStatus is nullable (null on the first row), which EF Core handles by itself.
            entity.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20);

            // The admin note is optional and capped, so the column is not nvarchar(max).
            entity.Property(h => h.Note).HasMaxLength(ClaimStatusHistory.MaxNoteLength);

            // Index on the foreign key: loading one claim's history reads just its rows.
            entity.HasIndex(h => h.ClaimId);
        });
    }
}
