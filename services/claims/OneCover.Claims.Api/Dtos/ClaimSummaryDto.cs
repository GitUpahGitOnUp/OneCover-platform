using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Dtos;

/// <summary>The numbers the admin dashboard shows for claims. Built by the server, so the web app does no counting.</summary>
/// <param name="TotalClaims">How many claims exist in all.</param>
/// <param name="ClaimsByStatus">One entry for EVERY status (zero included), in workflow order, so the dashboard can draw them directly.</param>
/// <param name="AwaitingPayoutCount">
/// Approved claims that Disbursements has not confirmed receiving. The dashboard should show this as a warning
/// (for example a red badge): the claim says Approved but the member may not be paid.
/// </param>
public record ClaimSummaryDto(
    int TotalClaims,
    IReadOnlyList<ClaimStatusCountDto> ClaimsByStatus,
    int AwaitingPayoutCount);

/// <summary>How many claims currently have one status.</summary>
/// <param name="Status">The status.</param>
/// <param name="Count">How many claims have it right now.</param>
public record ClaimStatusCountDto(ClaimStatus Status, int Count);
