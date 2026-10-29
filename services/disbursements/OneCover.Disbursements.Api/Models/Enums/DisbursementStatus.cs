// A "namespace" is a folder-like label for code so names don't collide.
// The file-scoped form (ending in ;) applies to everything in this file.
namespace OneCover.Disbursements.Api.Models.Enums;

/// <summary>
/// Where a disbursement is in its life. It starts as Pending and moves to exactly one of
/// Paid or Failed, and never moves again.
/// </summary>
public enum DisbursementStatus
{
    /// <summary>Created when a claim is approved. The money has not been sent yet.</summary>
    Pending,

    /// <summary>The money was sent to the member.</summary>
    Paid,

    /// <summary>The payout did not go through. The Reason is stored on the disbursement.</summary>
    Failed
}
