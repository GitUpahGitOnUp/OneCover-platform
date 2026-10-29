using OneCover.Disbursements.Api.Errors;
using OneCover.Disbursements.Api.Models.Enums;

namespace OneCover.Disbursements.Api.Models;

/// <summary>
/// A payout of benefit money to a member for one approved claim.
/// Created as Pending when Claims publishes ClaimApproved, then marked Paid or Failed by an admin.
/// </summary>
public class Disbursement
{
    /// <summary>Database primary key. "set" is public so EF Core can fill it in after saving.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The member being paid. This is the user's id from the Identity service.
    /// It is just a number: we have no User object here because that data belongs to another service.
    /// "init" means it can be set when the object is created and never changed after that.
    /// </summary>
    public int MemberId { get; init; }

    /// <summary>
    /// The approved claim this payout is for. Claims owns the claim, so again it is only an id.
    /// Unique in the database: one claim can only ever produce one disbursement.
    /// </summary>
    public int ClaimId { get; init; }

    /// <summary>How much to pay. Copied from the event, so it never changes if the claim changes later.</summary>
    // decimal (not double) is the right type for money: it has no binary rounding errors.
    public decimal Amount { get; init; }

    /// <summary>The date the payout is due.</summary>
    // DateOnly is a date with no time of day, which is all we need here.
    public DateOnly DueDate { get; init; }

    /// <summary>When the money was sent. Only has a value once Status is Paid.</summary>
    // The "?" makes the type nullable: it can hold a date or be empty (null).
    // "private set" means only code inside this class can change it (via MarkPaid).
    public DateOnly? PaidDate { get; private set; }

    /// <summary>Why the payout failed. Only has a value once Status is Failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Current state. Starts as Pending. "private set" protects the rule that the status can only
    /// change through MarkPaid / MarkFailed, so it can never be left in an inconsistent state.
    /// </summary>
    public DisbursementStatus Status { get; private set; } = DisbursementStatus.Pending;

    // A PRIVATE constructor means other code can't write "new Disbursement()".
    // The only way to create one is the factory method below, so every disbursement starts valid.
    // EF Core is still allowed to use it when it loads rows from the database.
    private Disbursement()
    {
    }

    /// <summary>Creates the Pending disbursement for an approved claim.</summary>
    // "static" means you call it on the class itself: Disbursement.CreatePending(...).
    // This is called a "factory method". The "=> new() { ... }" form returns a new object
    // and sets its properties in the { } block (allowed here because init works during creation).
    public static Disbursement CreatePending(int claimId, int memberId, decimal amount, DateOnly dueDate) => new()
    {
        ClaimId = claimId,
        MemberId = memberId,
        Amount = amount,
        DueDate = dueDate
        // Status is already Pending by default, and PaidDate / FailureReason stay null.
    };

    /// <summary>Marks a Pending disbursement as paid on the given date. Throws if it isn't Pending.</summary>
    // The rule this class protects: Status and PaidDate change TOGETHER, and only from Pending.
    public void MarkPaid(DateOnly paidOn)
    {
        // Guard clause: refuse right away if the rule is broken, so the code below only runs when valid.
        if (Status != DisbursementStatus.Pending)
        {
            // $"..." is string interpolation: {Status} is replaced with the current status's name.
            throw new BusinessRuleException($"Only pending disbursements can be marked paid. This one is {Status}.");
        }

        Status = DisbursementStatus.Paid;
        PaidDate = paidOn;
    }

    /// <summary>Marks a Pending disbursement as failed and records why. Throws if it isn't Pending.</summary>
    public void MarkFailed(string reason)
    {
        if (Status != DisbursementStatus.Pending)
        {
            throw new BusinessRuleException($"Only pending disbursements can be marked failed. This one is {Status}.");
        }

        Status = DisbursementStatus.Failed;
        FailureReason = reason;   // PaidDate stays null: the money never went out.
    }
}
