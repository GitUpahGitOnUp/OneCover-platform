using OneCover.Claims.Api.Mapping;
using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Tests;

/// <summary>Tests that a BenefitClaim is turned into the right DTO.</summary>
public class ClaimMappingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static BenefitClaim NewClaim() =>
        BenefitClaim.Submit(
            memberId: 42,
            enrollmentId: 7,
            coverageStartDate: new DateOnly(2026, 1, 1),
            planBenefitPercent: 0.60m,
            planWeeklyMaximum: 1_500m,
            planEliminationDays: 7,
            reason: "Recovering from surgery",
            startDate: new DateOnly(2026, 10, 1),
            endDate: new DateOnly(2026, 10, 14),
            weeklyBenefit: 900m,
            totalBenefit: 900m,
            submittedAt: Now);

    [Fact]
    public void ToDto_CopiesEveryField()
    {
        var dto = NewClaim().ToDto();

        Assert.Equal(42, dto.MemberId);
        Assert.Equal(7, dto.EnrollmentId);
        Assert.Equal("Recovering from surgery", dto.Reason);
        Assert.Equal(new DateOnly(2026, 10, 1), dto.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 14), dto.EndDate);
        Assert.Equal(ClaimStatus.Submitted, dto.Status);
        Assert.Equal(0.60m, dto.PlanBenefitPercent);
        Assert.Equal(1_500m, dto.PlanWeeklyMaximum);
        Assert.Equal(7, dto.PlanEliminationDays);
        Assert.Equal(900m, dto.WeeklyBenefit);
        Assert.Equal(900m, dto.TotalBenefit);
        Assert.Equal(Now, dto.SubmittedAt);
    }

    [Fact]
    public void ToDto_OffersTheMovesTheWorkflowAllows()
    {
        var claim = NewClaim();

        Assert.Equal(ClaimWorkflow.NextStatuses(ClaimStatus.Submitted), claim.ToDto().NextStatuses);

        claim.MoveTo(ClaimStatus.InReview, changedByUserId: 900, note: null, changedAt: Now);

        Assert.Equal(ClaimWorkflow.NextStatuses(ClaimStatus.InReview), claim.ToDto().NextStatuses);
    }

    [Fact]
    public void ToDto_ListsTheHistoryOldestFirst()
    {
        var claim = NewClaim();
        claim.MoveTo(ClaimStatus.InReview, changedByUserId: 900, note: "Started", changedAt: Now.AddHours(1));
        claim.MoveTo(ClaimStatus.InfoRequested, changedByUserId: 901, note: "Need doctor's note", changedAt: Now.AddHours(2));

        var history = claim.ToDto().History;

        Assert.Equal(3, history.Count);
        Assert.Null(history[0].FromStatus);   // the first row: the claim did not exist before
        Assert.Equal(ClaimStatus.Submitted, history[0].ToStatus);
        Assert.Equal(42, history[0].ChangedByUserId);
        Assert.Equal(ClaimStatus.InReview, history[1].ToStatus);
        Assert.Equal("Started", history[1].Note);
        Assert.Equal(ClaimStatus.InfoRequested, history[2].ToStatus);
        Assert.Equal(901, history[2].ChangedByUserId);
    }
}
