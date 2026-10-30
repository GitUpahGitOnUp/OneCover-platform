namespace OneCover.Contracts.Events;

/// <summary>
/// Event that Claims sends to Disbursements after a claim is approved.
/// Disbursements reacts by creating one Pending disbursement for the claim.
/// </summary>
/// <remarks>
/// "record" is a C# type made for plain data. You list the properties once in the parentheses
/// (this is a "positional record"), and the compiler builds the constructor, read-only properties,
/// and value-based equality for you. Two ClaimApproved objects with the same values are
/// considered equal, which is handy for tests.
/// The properties are "init-only": they can be set when the object is created, and never changed after.
/// </remarks>
/// <param name="ClaimId">
/// Id of the approved claim in the Claims service.
/// Disbursements uses it to make sure one claim never creates two disbursements:
/// a unique index on this value makes a second delivery of the same event a no-op.
/// </param>
/// <param name="MemberId">
/// Id of the member who filed the claim. It matches the "sub" claim in the JWT issued by Identity,
/// and Identity's User.Id is an int, so this is an int too.
/// </param>
/// <param name="Amount">
/// Total benefit to pay out, already calculated by Claims when the claim was filed.
/// Disbursements copies it and never recalculates it. "decimal" is used because it is exact for money
/// (double and float can introduce rounding errors).
/// </param>
/// <param name="ApprovedOn">
/// Calendar date the claim was approved (no time of day). Disbursements adds 7 days to it
/// to get the due date. "DateOnly" holds just year-month-day and is sent as yyyy-MM-dd.
/// </param>
public record ClaimApproved(
    int ClaimId,
    int MemberId,
    decimal Amount,
    DateOnly ApprovedOn);
