using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Errors;
using OneCover.Claims.Api.Models.Enums;
using OneCover.Claims.Api.Services;

namespace OneCover.Claims.Tests;

/// <summary>
/// A stand-in for the real claim service, so endpoint tests can check what the controller passes to it and how it
/// reacts to what it returns or throws, without a database. It records the calls it receives.
/// </summary>
public class FakeClaimService : IClaimService
{
    /// <summary>The member id the controller passed in. It must have come from the token.</summary>
    public int? MemberIdSeen { get; private set; }

    /// <summary>The request body the controller passed to SubmitAsync.</summary>
    public SubmitClaimRequest? RequestSeen { get; private set; }

    public Task<ClaimDto> SubmitAsync(int memberId, SubmitClaimRequest request)
    {
        MemberIdSeen = memberId;
        RequestSeen = request;
        return Task.FromResult(new ClaimDto(
            Id: 11, MemberId: memberId, EnrollmentId: request.EnrollmentId, Reason: request.Reason,
            StartDate: request.StartDate, EndDate: request.EndDate, Status: ClaimStatus.Submitted,
            PlanBenefitPercent: 0.60m, PlanWeeklyMaximum: 1_500m, PlanEliminationDays: 7,
            WeeklyBenefit: 900m, TotalBenefit: 900m, SubmittedAt: DateTimeOffset.UnixEpoch,
            NextStatuses: [ClaimStatus.InReview], History: []));
    }

    public Task<List<ClaimDto>> ListForMemberAsync(int memberId, int page, int pageSize)
    {
        MemberIdSeen = memberId;
        return Task.FromResult(new List<ClaimDto>());
    }

    // Always "not found", like the real service for a claim that is missing or belongs to someone else.
    public Task<ClaimDto> GetForMemberAsync(int id, int memberId)
    {
        MemberIdSeen = memberId;
        throw new NotFoundException("Claim", id);
    }
}
