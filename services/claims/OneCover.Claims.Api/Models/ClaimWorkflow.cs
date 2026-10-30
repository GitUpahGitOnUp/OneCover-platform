using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Models;

/// <summary>
/// The claim status rulebook: which status a claim can move to from each status.
/// Submitted → InReview ⇄ InfoRequested; InReview → Approved | Denied; Approved/Denied → Closed.
/// The BenefitClaim entity (next PR) checks this before every status change.
/// </summary>
// "static class" = a class you never create with "new". Its members are called on the class itself:
// ClaimWorkflow.CanMove(...). It fits because the rulebook has no per-claim data, it is just rules.
public static class ClaimWorkflow
{
    // The rules, stored as DATA: a lookup table from "current status" to "statuses you may move to".
    // This is easier to read and change than a pile of if statements.
    // "private" = encapsulation: nothing outside this class can read or edit the table directly,
    // so the only way to ask a question is CanMove / NextStatuses below.
    // "static readonly" = one shared copy for the whole app, and the variable can't be re-pointed.
    // "[ ... ]" is a collection expression: a short way to write an array.
    private static readonly Dictionary<ClaimStatus, ClaimStatus[]> Allowed = new()
    {
        [ClaimStatus.Submitted]     = [ClaimStatus.InReview],
        [ClaimStatus.InReview]      = [ClaimStatus.InfoRequested, ClaimStatus.Approved, ClaimStatus.Denied],
        [ClaimStatus.InfoRequested] = [ClaimStatus.InReview],
        [ClaimStatus.Approved]      = [ClaimStatus.Closed],
        [ClaimStatus.Denied]        = [ClaimStatus.Closed],
        [ClaimStatus.Closed]        = []   // final: no moves out of Closed
    };

    /// <summary>True if a claim in status <paramref name="from"/> is allowed to move to <paramref name="to"/>.</summary>
    // "=>" is an expression-bodied method: a one-line method that returns the expression on its right.
    // TryGetValue looks up "from" without crashing if it is missing: a status with no entry (say, one added
    // to the enum later) means "no moves allowed", which gives a clean error instead of a KeyNotFoundException.
    // "out var next" receives the array when the lookup succeeds. "&&" only checks Contains when it did.
    public static bool CanMove(ClaimStatus from, ClaimStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>The statuses a claim can move to next. The admin UI uses this to fill its "Move to…" dropdown.</summary>
    // Array.AsReadOnly wraps the array so callers can read it but not change the real rules.
    // An unknown status returns an empty list ("[]").
    public static IReadOnlyList<ClaimStatus> NextStatuses(ClaimStatus from) =>
        Allowed.TryGetValue(from, out var next) ? Array.AsReadOnly(next) : [];
}
