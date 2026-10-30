using OneCover.Disbursements.Api.Models.Enums;

namespace OneCover.Disbursements.Api.Dtos;

/// <summary>What the API returns for one disbursement. Never exposes the entity itself.</summary>
// "record" = a class built to carry data. The values in ( ) become read-only properties,
// and the compiler writes the constructor for us. Called "positional record" syntax.
// <param> tags document each property, since a record's properties are declared in the ( ).
/// <param name="Id">The disbursement's id.</param>
/// <param name="MemberId">The member being paid.</param>
/// <param name="ClaimId">The approved claim this payout is for.</param>
/// <param name="Amount">How much is being paid.</param>
/// <param name="DueDate">The date the payout is due.</param>
/// <param name="PaidDate">When the money was sent. Null unless the status is Paid.</param>
/// <param name="Status">Pending, Paid or Failed.</param>
/// <param name="FailureReason">Why the payout failed. Null unless the status is Failed.</param>
public record DisbursementDto(
    int Id,
    int MemberId,
    int ClaimId,
    decimal Amount,
    DateOnly DueDate,
    DateOnly? PaidDate,
    DisbursementStatus Status,
    string? FailureReason);
