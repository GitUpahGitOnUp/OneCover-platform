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
    /// <summary>Database primary key. "set" is public so EF Core can fill it in after saving.</summary>
    public int Id { get; set; }

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
        // AddYears(1) moves the start date forward one year. Real STD plans cap far lower, so this is generous.
        if (endDate > startDate.AddYears(1))
        {
            throw new BusinessRuleException("A single claim can't cover more than one year.");
        }

        var claim = new BenefitClaim
        {
            MemberId = memberId,
            EnrollmentId = enrollmentId,
            Reason = reason.Trim(),   // Trim() removes spaces from both ends of the text
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
        claim._history.Add(new ClaimStatusHistory
        {
            FromStatus = null,
            ToStatus = ClaimStatus.Submitted,
            ChangedByUserId = memberId,
            ChangedAt = submittedAt
        });

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

        // 2. Record the change BEFORE updating Status, so FromStatus still holds the old value.
        _history.Add(new ClaimStatusHistory
        {
            FromStatus = Status,
            ToStatus = newStatus,
            ChangedByUserId = changedByUserId,
            ChangedAt = changedAt,
            // A blank or whitespace-only note is stored as null instead of an empty string.
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        });

        // 3. Only now change the status. The private setter means this class is the ONLY place that can.
        Status = newStatus;
    }
}
