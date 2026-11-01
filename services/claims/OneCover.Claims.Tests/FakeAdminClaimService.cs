using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Errors;
using OneCover.Claims.Api.Models.Enums;
using OneCover.Claims.Api.Services;

namespace OneCover.Claims.Tests;

/// <summary>
/// A stand-in for the real admin claim service, so endpoint tests can check what the controller passes to it
/// without a database. It records the calls it receives.
/// </summary>
public class FakeAdminClaimService : IAdminClaimService
{
    /// <summary>The admin id the controller passed to UpdateStatusAsync. It must have come from the token.</summary>
    public int? AdminIdSeen { get; private set; }

    /// <summary>The request body the controller passed to UpdateStatusAsync.</summary>
    public UpdateClaimStatusRequest? RequestSeen { get; private set; }

    /// <summary>The status filter the controller passed to ListAllAsync.</summary>
    public ClaimStatus? FilterSeen { get; private set; }

    /// <summary>The awaiting-payout flag the controller passed to ListAllAsync.</summary>
    public bool? AwaitingPayoutSeen { get; private set; }

    /// <summary>The claim id the controller passed to ResendPayoutAsync.</summary>
    public int? ResentId { get; private set; }

    /// <summary>When set, ResendPayoutAsync throws this instead of succeeding.</summary>
    public Exception? ResendFailure { get; set; }

    public Task<List<ClaimDto>> ListAllAsync(ClaimStatus? status, int page, int pageSize, bool awaitingPayoutOnly = false)
    {
        FilterSeen = status;
        AwaitingPayoutSeen = awaitingPayoutOnly;
        return Task.FromResult(new List<ClaimDto>());
    }

    public Task<ClaimSummaryDto> GetSummaryAsync() =>
        Task.FromResult(new ClaimSummaryDto(
            TotalClaims: 5,
            ClaimsByStatus: [new ClaimStatusCountDto(ClaimStatus.Approved, 5)],
            AwaitingPayoutCount: 2));

    public Task<ClaimDto> ResendPayoutAsync(int id)
    {
        ResentId = id;
        if (ResendFailure is not null)
        {
            throw ResendFailure;
        }

        return Task.FromResult(new ClaimDto(
            Id: id, MemberId: 1, EnrollmentId: 1, Reason: "Surgery",
            StartDate: new DateOnly(2026, 10, 1), EndDate: new DateOnly(2026, 10, 14), Status: ClaimStatus.Approved,
            PlanBenefitPercent: 0.60m, PlanWeeklyMaximum: 1_500m, PlanEliminationDays: 7,
            WeeklyBenefit: 900m, TotalBenefit: 900m, SubmittedAt: DateTimeOffset.UnixEpoch,
            PayoutRequestedAt: DateTimeOffset.UnixEpoch, AwaitingPayout: false,
            NextStatuses: [ClaimStatus.Closed], History: []));
    }

    public Task<ClaimDto> GetAsync(int id) => throw new NotFoundException("Claim", id);

    public Task<ClaimDto> UpdateStatusAsync(int id, int adminId, UpdateClaimStatusRequest request)
    {
        AdminIdSeen = adminId;
        RequestSeen = request;
        return Task.FromResult(new ClaimDto(
            Id: id, MemberId: 1, EnrollmentId: 1, Reason: "Surgery",
            StartDate: new DateOnly(2026, 10, 1), EndDate: new DateOnly(2026, 10, 14), Status: request.Status,
            PlanBenefitPercent: 0.60m, PlanWeeklyMaximum: 1_500m, PlanEliminationDays: 7,
            WeeklyBenefit: 900m, TotalBenefit: 900m, SubmittedAt: DateTimeOffset.UnixEpoch,
            PayoutRequestedAt: null, AwaitingPayout: false,
            NextStatuses: [], History: []));
    }
}
