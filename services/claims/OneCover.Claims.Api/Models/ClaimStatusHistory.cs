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
    /// <summary>Database primary key. "set" is public so EF Core can fill it in after saving.</summary>
    public int Id { get; set; }

    /// <summary>The claim this row belongs to. EF Core fills it in when the claim is saved.</summary>
    public int ClaimId { get; set; }

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
