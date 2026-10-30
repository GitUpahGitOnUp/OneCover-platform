using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Models;

/// <summary>
/// One row of a claim's audit trail: a single status change, who made it and when.
/// Every status move writes one of these, including the first one when the claim is filed.
/// </summary>
// Only BenefitClaim creates these rows (in Submit and MoveTo), so nothing outside the claim can
// forge or edit the trail. "init" properties can be set when the row is created and never changed after.
public class ClaimStatusHistory
{
    /// <summary>Longest admin note allowed. The database column uses the same limit.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>Database primary key. "private set": only EF Core (which uses reflection) fills it in after saving.</summary>
    public int Id { get; private set; }

    /// <summary>The claim this row belongs to. EF Core fills it in when the claim is saved.</summary>
    public int ClaimId { get; private set; }

    // A PRIVATE constructor means other code can't write "new ClaimStatusHistory { ... }", so the audit trail
    // can't be forged from a controller or service. EF Core can still call it when loading rows.
    private ClaimStatusHistory()
    {
    }

    /// <summary>
    /// Builds one audit row. Called only by <see cref="BenefitClaim"/> (in Submit and MoveTo).
    /// </summary>
    // "internal" = visible inside this project only. It is the one door for creating a row, with a name
    // that makes any other use stand out in code review.
    internal static ClaimStatusHistory Record(
        ClaimStatus? fromStatus,
        ClaimStatus toStatus,
        int changedByUserId,
        DateTimeOffset changedAt,
        string? note)
    {
        return new ClaimStatusHistory
        {
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedByUserId = changedByUserId,
            ChangedAt = changedAt,
            Note = note
        };
    }

    /// <summary>The status before the change. Null for the first row, because the claim did not exist yet.</summary>
    // The "?" makes the type nullable: it can hold a value or be empty (null).
    public ClaimStatus? FromStatus { get; init; }

    /// <summary>The status after the change.</summary>
    public ClaimStatus ToStatus { get; init; }

    /// <summary>
    /// The user who made the change: the member when filing, an admin for later moves.
    /// Just an id from the JWT, because the user belongs to the Identity service.
    /// </summary>
    public int ChangedByUserId { get; init; }

    /// <summary>When the change happened. Comes from TimeProvider, never from the client.</summary>
    // DateTimeOffset is a date and time that also remembers its UTC offset, so it is unambiguous.
    public DateTimeOffset ChangedAt { get; init; }

    /// <summary>An optional admin note, such as what information was requested. Null when there is none.</summary>
    public string? Note { get; init; }
}
