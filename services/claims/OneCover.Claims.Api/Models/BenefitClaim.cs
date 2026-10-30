using OneCover.Claims.Api.Errors;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Models;

/// <summary>
/// A member's request for disability benefits (STD or LTD). Named BenefitClaim, not Claim,
/// to avoid a clash with System.Security.Claims.Claim, which the JWT code uses.
/// </summary>
/// <remarks>
/// This class protects its own rules (encapsulation): the status, the amounts and the history can only
/// change through <see cref="Submit"/> and <see cref="MoveTo"/>, so a claim can never be left invalid.
/// It holds only ids for things other services own (the member and the enrollment), never their objects.
/// </remarks>
public class BenefitClaim
{
    /// <summary>Longest reason allowed. The database column uses the same limit.</summary>
    // const = a value fixed at compile time. Shared by Submit and the DbContext so they cannot drift apart.
    public const int MaxReasonLength = 1000;

    /// <summary>Database primary key. "private set": only EF Core (which uses reflection) fills it in after saving.</summary>
    public int Id { get; private set; }

    /// <summary>
    /// The member who filed the claim. This is the user's id from the Identity service (the JWT "sub").
    /// Stored directly so "my claims" can filter without asking another service.
    /// </summary>
    // "init" = can be set when the object is created and never changed after that.
    public int MemberId { get; init; }

    /// <summary>The Enrollment-service enrollment this claim is against. Just an id: Enrollment owns the data.</summary>
    public int EnrollmentId { get; init; }

    /// <summary>Why the member is on leave, in their own words.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>First day of leave.</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>Last day of leave.</summary>
    public DateOnly EndDate { get; init; }

    /// <summary>
    /// Current workflow status. Starts as Submitted. "private set" means only code inside this class can
    /// change it (through <see cref="MoveTo"/>), and outside code can still read it.
    /// EF Core can also set it when it loads a claim from the database.
    /// </summary>
    public ClaimStatus Status { get; private set; } = ClaimStatus.Submitted;

    // ---- Plan terms: a snapshot of the plan as it was when the claim was filed. ----
    // CLAUDE.md rule: "A claim keeps the plan terms it was filed under, even if the plan changes later."
    // These are copied from Enrollment when the claim is filed and never change after.

    /// <summary>The plan's benefit as a fraction when the claim was filed, e.g. 0.60 for 60%.</summary>
    public decimal PlanBenefitPercent { get; init; }

    /// <summary>The plan's weekly maximum when the claim was filed.</summary>
    public decimal PlanWeeklyMaximum { get; init; }

    /// <summary>The plan's unpaid waiting days when the claim was filed (7 for STD, 90 for LTD).</summary>
    public int PlanEliminationDays { get; init; }

    /// <summary>
    /// Weekly benefit, calculated once on the server when the claim was filed. The request never carries an amount.
    /// </summary>
    public decimal WeeklyBenefit { get; init; }

    /// <summary>Total benefit for the whole leave after the unpaid waiting days, calculated once at filing.</summary>
    public decimal TotalBenefit { get; init; }

    /// <summary>When the claim was filed. Comes from TimeProvider, never from the client.</summary>
    public DateTimeOffset SubmittedAt { get; init; }

    /// <summary>
    /// Version stamp that SQL Server changes automatically on every update ("rowversion").
    /// EF Core adds it to the WHERE clause of each UPDATE, so if another admin changed the claim after
    /// we loaded it, our save fails with DbUpdateConcurrencyException instead of silently overwriting them.
    /// This matters here because two admins could otherwise approve and deny the same claim at the same moment.
    /// </summary>
    // byte[] = raw bytes. "= []" starts as an empty array; the database fills in the real value.
    public byte[] RowVersion { get; private set; } = [];

    // The real list of history rows. "private" so only this class can add to it.
    // "readonly" means the field can't be pointed at a different list, though items can still be added.
    // "[]" is an empty collection. EF Core finds this list by its naming convention (_ + the property name).
    private readonly List<ClaimStatusHistory> _history = [];

    /// <summary>The audit trail, oldest first. Read-only: it has no Add method, so only the claim can extend it.</summary>
    // IReadOnlyCollection exposes the list for reading only. "=>" returns the private list on each read.
    public IReadOnlyCollection<ClaimStatusHistory> History => _history;

    // A PRIVATE constructor means other code can't write "new BenefitClaim()".
    // The only way to create a claim is Submit below, so every claim starts valid.
    // EF Core is still allowed to call it when it loads rows from the database.
    private BenefitClaim()
    {
    }

    /// <summary>
    /// Creates a new claim in Submitted status, with its first history row.
    /// Throws <see cref="BusinessRuleException"/> if the end date is before the start date,
    /// the leave begins before coverage did, or one claim covers more than a year.
    /// </summary>
    /// <param name="memberId">The member filing the claim (from the JWT).</param>
    /// <param name="enrollmentId">The active STD or LTD enrollment the claim is against.</param>
    /// <param name="coverageStartDate">The date the member's coverage began (the enrollment date).</param>
    /// <param name="planBenefitPercent">The plan's benefit fraction, copied onto the claim.</param>
    /// <param name="planWeeklyMaximum">The plan's weekly maximum, copied onto the claim.</param>
    /// <param name="planEliminationDays">The plan's unpaid waiting days, copied onto the claim.</param>
    /// <param name="reason">Why the member is on leave.</param>
    /// <param name="startDate">First day of leave.</param>
    /// <param name="endDate">Last day of leave.</param>
    /// <param name="weeklyBenefit">Weekly benefit worked out by the benefit calculator.</param>
    /// <param name="totalBenefit">Total benefit worked out by the benefit calculator.</param>
    /// <param name="submittedAt">The current time, from TimeProvider.</param>
    // "static" = called on the class itself: BenefitClaim.Submit(...). This is a FACTORY METHOD: it checks
    // the rules first, then returns a new claim. Callers should use named arguments (a CLAUDE.md convention),
    // because there are many parameters and several of them are the same type.
    public static BenefitClaim Submit(
        int memberId,
        int enrollmentId,
        DateOnly coverageStartDate,
        decimal planBenefitPercent,
        decimal planWeeklyMaximum,
        int planEliminationDays,
        string reason,
        DateOnly startDate,
        DateOnly endDate,
        decimal weeklyBenefit,
        decimal totalBenefit,
        DateTimeOffset submittedAt)
    {
        // Guard clauses: check each rule first and throw straight away, so the code below only runs for a valid claim.
        // The ids must be real ids (database ids start at 1), so a default 0 from a bug is refused.
        if (memberId <= 0 || enrollmentId <= 0)
        {
            throw new BusinessRuleException("A claim needs a valid member id and enrollment id.");
        }

        // The plan snapshot must make sense on its own. A percent is a fraction: 60 instead of 0.60 would also
        // overflow the decimal(5,4) column and surface as a 500, so it is refused here with a clear message.
        if (planBenefitPercent is <= 0m or > 1m)
        {
            throw new BusinessRuleException("The plan's benefit percent must be above 0 and at most 1 (0.60 means 60%).");
        }

        if (planWeeklyMaximum < 0 || planEliminationDays < 0)
        {
            throw new BusinessRuleException("The plan's weekly maximum and elimination days can't be negative.");
        }

        // Trim once and reuse it. IsNullOrWhiteSpace is true for null, "" and "   ". The length limit matches
        // the database column, so a too-long reason becomes a clear 409 instead of a database error.
        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (trimmedReason.Length == 0 || trimmedReason.Length > MaxReasonLength)
        {
            throw new BusinessRuleException($"A claim needs a reason of 1 to {MaxReasonLength} characters.");
        }

        if (endDate < startDate)
        {
            throw new BusinessRuleException("End date can't be before the start date.");
        }

        // A claim can only cover time the member was covered. This also blocks nonsense dates like 0001-01-01.
        if (startDate < coverageStartDate)
        {
            throw new BusinessRuleException("A claim can't start before the coverage began.");
        }

        // Without an upper bound, an end date like 9999-12-31 would calculate a multi-million-dollar benefit.
        // Both the start and end day count as leave days, so a leave starting 2026-03-01 reaches one full year
        // on 2027-02-28. An end date on or after startDate.AddYears(1) (2027-03-01) would be a year and a day.
        // Real STD plans cap far lower, so this is generous.
        if (endDate >= startDate.AddYears(1))
        {
            throw new BusinessRuleException("A single claim can't cover more than one year.");
        }

        // The amounts come from the benefit calculator, but this class does not trust them blindly: it checks
        // that they fit the plan snapshot it is about to store, so a wrong amount can't reach Disbursements.
        // The weekly benefit can never be negative or above the plan's weekly maximum.
        if (weeklyBenefit < 0 || weeklyBenefit > planWeeklyMaximum)
        {
            throw new BusinessRuleException("The weekly benefit must be between 0 and the plan's weekly maximum.");
        }

        // The total can never be negative or more than the weekly benefit spread over EVERY day of the leave
        // (the real total is lower, because the first elimination days are unpaid). Both end days count, hence + 1.
        var leaveDays = endDate.DayNumber - startDate.DayNumber + 1;
        var mostPossibleTotal = Math.Round(weeklyBenefit / 7m * leaveDays, 2, MidpointRounding.AwayFromZero);
        if (totalBenefit < 0 || totalBenefit > mostPossibleTotal)
        {
            throw new BusinessRuleException("The total benefit can't be negative or more than the weekly benefit over the whole leave.");
        }

        var claim = new BenefitClaim
        {
            MemberId = memberId,
            EnrollmentId = enrollmentId,
            Reason = trimmedReason,
            StartDate = startDate,
            EndDate = endDate,
            PlanBenefitPercent = planBenefitPercent,
            PlanWeeklyMaximum = planWeeklyMaximum,
            PlanEliminationDays = planEliminationDays,
            WeeklyBenefit = weeklyBenefit,
            TotalBenefit = totalBenefit,
            SubmittedAt = submittedAt
            // Status already defaults to Submitted.
        };

        // The first audit row. FromStatus is null because the claim did not exist before, and the member
        // filed it. We can add to _history here because we are INSIDE the class. On save, EF Core inserts
        // the claim first and then this row, filling in its ClaimId.
        claim._history.Add(ClaimStatusHistory.Record(
            fromStatus: null,
            toStatus: ClaimStatus.Submitted,
            changedByUserId: memberId,
            changedAt: submittedAt,
            note: null));

        return claim;
    }

    /// <summary>
    /// Moves the claim to a new status if the workflow allows it, and records the change in <see cref="History"/>.
    /// Throws <see cref="BusinessRuleException"/> for a move the workflow doesn't allow (for example Closed → InReview).
    /// </summary>
    /// <param name="newStatus">The status to move to.</param>
    /// <param name="changedByUserId">The admin making the change (from the JWT).</param>
    /// <param name="note">An optional note. Blank text is stored as no note.</param>
    /// <param name="changedAt">The current time, from TimeProvider.</param>
    public void MoveTo(ClaimStatus newStatus, int changedByUserId, string? note, DateTimeOffset changedAt)
    {
        // 1. Ask the rulebook. An invalid move changes nothing, because we throw before touching any state.
        if (!ClaimWorkflow.CanMove(Status, newStatus))
        {
            throw new BusinessRuleException($"Can't move a claim from {Status} to {newStatus}.");
        }

        // The actor must be a real user id from the JWT, so the audit trail never records "user 0".
        if (changedByUserId <= 0)
        {
            throw new BusinessRuleException("A status change needs a valid user id.");
        }

        // Trim once. A blank or whitespace-only note is stored as null instead of an empty string.
        // The note must also fit its database column, so a too-long one is a clear 409 instead of a database error.
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is not null && trimmedNote.Length > ClaimStatusHistory.MaxNoteLength)
        {
            throw new BusinessRuleException($"A note can be at most {ClaimStatusHistory.MaxNoteLength} characters.");
        }

        // 2. Record the change BEFORE updating Status, so FromStatus still holds the old value.
        _history.Add(ClaimStatusHistory.Record(
            fromStatus: Status,
            toStatus: newStatus,
            changedByUserId: changedByUserId,
            changedAt: changedAt,
            note: trimmedNote));

        // 3. Only now change the status. The private setter means this class is the ONLY place that can.
        Status = newStatus;
    }
}
