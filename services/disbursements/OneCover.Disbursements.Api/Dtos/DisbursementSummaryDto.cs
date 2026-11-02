namespace OneCover.Disbursements.Api.Dtos;

/// <summary>The totals the admin dashboard shows for disbursements. Built by the server, so the web app does no adding up.</summary>
// "record" = a class built to carry data; the values in ( ) become read-only properties.
/// <param name="TotalCount">How many disbursements exist in all.</param>
/// <param name="PendingCount">How many are waiting to be paid.</param>
/// <param name="PaidCount">How many have been paid.</param>
/// <param name="FailedCount">How many payouts failed.</param>
/// <param name="PendingAmount">Money still waiting to be paid out.</param>
/// <param name="PaidAmount">Money already paid out.</param>
/// <param name="FailedAmount">Money from payouts that failed and still need attention.</param>
public record DisbursementSummaryDto(
    int TotalCount,
    int PendingCount,
    int PaidCount,
    int FailedCount,
    decimal PendingAmount,
    decimal PaidAmount,
    decimal FailedAmount);
