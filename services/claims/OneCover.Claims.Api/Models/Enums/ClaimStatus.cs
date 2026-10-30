namespace OneCover.Claims.Api.Models.Enums;

/// <summary>
/// Where a claim is in the review workflow. Which moves are allowed is decided by <see cref="ClaimWorkflow"/>.
/// </summary>
// An enum is a fixed list of named values. A ClaimStatus variable can only ever hold one of these six,
// so a typo like "Aproved" is caught by the compiler instead of slipping through as a string.
public enum ClaimStatus
{
    /// <summary>The member filed the claim and nobody has looked at it yet.</summary>
    Submitted,

    /// <summary>An admin is reviewing the claim.</summary>
    InReview,

    /// <summary>The admin needs more information from the member before deciding.</summary>
    InfoRequested,

    /// <summary>The claim was approved. This is what triggers the ClaimApproved event to Disbursements.</summary>
    Approved,

    /// <summary>The claim was denied.</summary>
    Denied,

    /// <summary>Final state after a decision. Nothing can happen to the claim after this.</summary>
    Closed
}
