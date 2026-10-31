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

    public Task<List<ClaimDto>> ListAllAsync(ClaimStatus? status, int page, int pageSize)
    {
        FilterSeen = status;
        return Task.FromResult(new List<ClaimDto>());
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
            NextStatuses: [], History: []));
    }
}
