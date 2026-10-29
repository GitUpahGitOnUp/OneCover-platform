using OneCover.Disbursements.Api.Models.Enums;

namespace OneCover.Disbursements.Api.Dtos;

/// <summary>The body of PUT /api/admin/disbursements/{id}/status.</summary>
/// <param name="Status">The new status. Only Paid or Failed are allowed (a Pending one is rejected).</param>
/// <param name="Reason">Why it failed. Required when Status is Failed, ignored when Paid.</param>
public record UpdateDisbursementStatusRequest(DisbursementStatus Status, string? Reason);
