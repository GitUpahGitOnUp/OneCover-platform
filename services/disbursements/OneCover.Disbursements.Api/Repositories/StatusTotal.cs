using OneCover.Disbursements.Api.Models.Enums;

namespace OneCover.Disbursements.Api.Repositories;

/// <summary>How many disbursements have one status, and what they add up to. One row of the dashboard query.</summary>
/// <param name="Status">The status these numbers are for.</param>
/// <param name="Count">How many disbursements have that status.</param>
/// <param name="Amount">The sum of their amounts.</param>
public record StatusTotal(DisbursementStatus Status, int Count, decimal Amount);
