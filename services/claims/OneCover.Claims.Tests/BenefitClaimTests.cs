using OneCover.Claims.Api.Errors;
using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Tests;

/// <summary>Unit tests for the BenefitClaim entity: filing rules, the status workflow and the audit trail.</summary>
public class BenefitClaimTests
{
    // A fixed "now" so tests don't depend on the real clock.
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly CoverageStart = new(2026, 1, 1);

    // A helper that builds a valid claim. Each test changes only the argument it cares about
    // (the others keep their defaults), which keeps the tests short and shows what each one is testing.
    // The "?" parameters with "= null" are optional: when a test leaves one out, "??" fills in the default.
    private static BenefitClaim Submit(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string reason = "Recovering from surgery",
        int memberId = 42,
        int enrollmentId = 7,
        decimal planBenefitPercent = 0.60m,
        decimal planWeeklyMaximum = 1_500m,
        int planEliminationDays = 7,
        decimal weeklyBenefit = 900m,
        decimal totalBenefit = 900m)
    {
        return BenefitClaim.Submit(
            memberId: memberId,
            enrollmentId: enrollmentId,
            coverageStartDate: CoverageStart,
            planBenefitPercent: planBenefitPercent,
            planWeeklyMaximum: planWeeklyMaximum,
            planEliminationDays: planEliminationDays,
            reason: reason,
            startDate: startDate ?? new DateOnly(2026, 10, 1),
            endDate: endDate ?? new DateOnly(2026, 10, 14),
            weeklyBenefit: weeklyBenefit,
            totalBenefit: totalBenefit,
            submittedAt: Now);
    }

    [Fact]
    public void Submit_CreatesASubmittedClaim_WithItsAmountsAndPlanTerms()
    {
        var claim = Submit();

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal(42, claim.MemberId);
        Assert.Equal(7, claim.EnrollmentId);
        Assert.Equal(900m, claim.WeeklyBenefit);
        Assert.Equal(900m, claim.TotalBenefit);
        Assert.Equal(0.60m, claim.PlanBenefitPercent);
        Assert.Equal(1_500m, claim.PlanWeeklyMaximum);
        Assert.Equal(7, claim.PlanEliminationDays);
        Assert.Equal(Now, claim.SubmittedAt);
    }

    [Fact]
    public void Submit_WritesTheFirstHistoryRow_FiledByTheMember()
    {
        var claim = Submit();

        var row = Assert.Single(claim.History);
        Assert.Null(row.FromStatus);
        Assert.Equal(ClaimStatus.Submitted, row.ToStatus);
        Assert.Equal(42, row.ChangedByUserId);
        Assert.Equal(Now, row.ChangedAt);
    }

    [Fact]
    public void Submit_TrimsTheReason()
    {
        var claim = Submit(reason: "  Broken leg  ");

        Assert.Equal("Broken leg", claim.Reason);
    }

    // [Theory] runs the same test once per row. A blank reason is refused whether it is empty or only spaces.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_Throws_WhenTheReasonIsBlank(string reason)
    {
        Assert.Throws<BusinessRuleException>(() => Submit(reason: reason));
    }

    [Fact]
    public void Submit_Throws_WhenTheReasonIsLongerThanTheDatabaseColumn()
    {
        var tooLong = new string('x', BenefitClaim.MaxReasonLength + 1);

        Assert.Throws<BusinessRuleException>(() => Submit(reason: tooLong));
    }

    [Fact]
    public void MoveTo_Throws_AndChangesNothing_WhenTheNoteIsTooLong()
    {
        var claim = Submit();
        var tooLong = new string('x', ClaimStatusHistory.MaxNoteLength + 1);

        Assert.Throws<BusinessRuleException>(() =>
            claim.MoveTo(ClaimStatus.InReview, changedByUserId: 1, note: tooLong, changedAt: Now));

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Single(claim.History);
    }

    [Fact]
    public void Submit_Throws_WhenEndDateIsBeforeStartDate()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            Submit(startDate: new DateOnly(2026, 10, 10), endDate: new DateOnly(2026, 10, 9)));

        Assert.Equal("End date can't be before the start date.", ex.Message);
    }

    [Fact]
    public void Submit_Throws_WhenLeaveStartsBeforeCoverageBegan()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            Submit(startDate: CoverageStart.AddDays(-1), endDate: CoverageStart.AddDays(10)));

        Assert.Equal("A claim can't start before the coverage began.", ex.Message);
    }

    [Fact]
    public void Submit_AllowsALeaveStartingOnTheFirstDayOfCoverage()
    {
        var claim = Submit(startDate: CoverageStart, endDate: CoverageStart.AddDays(10));

        Assert.Equal(CoverageStart, claim.StartDate);
    }

    [Fact]
    public void Submit_Throws_WhenTheClaimCoversMoreThanOneYear()
    {
        var start = new DateOnly(2026, 3, 1);

        // 2026-03-01 to 2027-03-01 counts BOTH end days, which is a year and a day.
        var ex = Assert.Throws<BusinessRuleException>(() =>
            Submit(startDate: start, endDate: start.AddYears(1)));

        Assert.Equal("A single claim can't cover more than one year.", ex.Message);
    }

    [Fact]
    public void Submit_AllowsALeaveOfAFullYearCountingBothEndDays()
    {
        var start = new DateOnly(2026, 3, 1);

        // 2026-03-01 to 2027-02-28 is exactly 365 days with both ends counted: one year.
        var claim = Submit(startDate: start, endDate: start.AddYears(1).AddDays(-1));

        Assert.Equal(new DateOnly(2027, 2, 28), claim.EndDate);
    }

    // [Theory] runs the same test once per row. Each row is a bad plan snapshot or id that must be refused.
    [Theory]
    [InlineData(0, 7)]    // memberId of 0 (a default from a bug)
    [InlineData(42, 0)]   // enrollmentId of 0
    [InlineData(-1, 7)]
    public void Submit_Throws_ForInvalidIds(int memberId, int enrollmentId)
    {
        Assert.Throws<BusinessRuleException>(() => Submit(memberId: memberId, enrollmentId: enrollmentId));
    }

    [Theory]
    [InlineData(60)]      // 60 passed instead of 0.60
    [InlineData(1.01)]
    [InlineData(0)]       // a plan that pays nothing can't be claimed against
    [InlineData(-0.5)]
    public void Submit_Throws_WhenThePlanPercentIsNotAFractionAboveZero(double percent)
    {
        // InlineData can't hold decimal values, so the rows use double and are converted here.
        Assert.Throws<BusinessRuleException>(() => Submit(planBenefitPercent: (decimal)percent));
    }

    [Fact]
    public void Submit_Throws_WhenThePlanWeeklyMaximumIsNegative()
    {
        Assert.Throws<BusinessRuleException>(() => Submit(planWeeklyMaximum: -1m));
    }

    [Fact]
    public void Submit_Throws_WhenThePlanEliminationDaysAreNegative()
    {
        Assert.Throws<BusinessRuleException>(() => Submit(planEliminationDays: -7));
    }

    [Fact]
    public void Submit_Throws_WhenTheWeeklyBenefitIsAboveThePlanMaximum()
    {
        Assert.Throws<BusinessRuleException>(() =>
            Submit(planWeeklyMaximum: 1_500m, weeklyBenefit: 1_500.01m, totalBenefit: 100m));
    }

    [Fact]
    public void Submit_Throws_WhenTheWeeklyBenefitIsNegative()
    {
        Assert.Throws<BusinessRuleException>(() => Submit(weeklyBenefit: -1m, totalBenefit: 0m));
    }

    [Fact]
    public void Submit_Throws_WhenTheTotalBenefitIsNegative()
    {
        Assert.Throws<BusinessRuleException>(() => Submit(totalBenefit: -1m));
    }

    [Fact]
    public void Submit_Throws_WhenTheTotalBenefitIsMoreThanTheWeeklyBenefitOverTheWholeLeave()
    {
        // Oct 1-14 is 14 days = 2 weeks, so at 900 a week the most possible is 1,800.
        Assert.Throws<BusinessRuleException>(() => Submit(totalBenefit: 1_800.01m));
    }

    [Fact]
    public void Submit_AllowsATotalAtTheMostPossibleForTheLeave()
    {
        var claim = Submit(totalBenefit: 1_800m);

        Assert.Equal(1_800m, claim.TotalBenefit);
    }

    [Fact]
    public void MoveTo_Throws_AndChangesNothing_WhenTheUserIdIsNotReal()
    {
        var claim = Submit();

        Assert.Throws<BusinessRuleException>(() =>
            claim.MoveTo(ClaimStatus.InReview, changedByUserId: 0, note: null, changedAt: Now));

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Single(claim.History);
    }

    [Fact]
    public void MoveTo_ChangesTheStatus_AndRecordsWhoAndWhen()
    {
        var claim = Submit();
        var later = Now.AddHours(2);

        claim.MoveTo(ClaimStatus.InReview, changedByUserId: 1, note: null, changedAt: later);

        Assert.Equal(ClaimStatus.InReview, claim.Status);
        Assert.Equal(2, claim.History.Count);

        // .Last() = the most recent history row (the one MoveTo just added).
        var row = claim.History.Last();
        Assert.Equal(ClaimStatus.Submitted, row.FromStatus);
        Assert.Equal(ClaimStatus.InReview, row.ToStatus);
        Assert.Equal(1, row.ChangedByUserId);
        Assert.Equal(later, row.ChangedAt);
    }

    [Fact]
    public void MoveTo_Throws_AndChangesNothing_ForAMoveTheWorkflowForbids()
    {
        var claim = Submit();

        // Submitted -> Approved skips the review, so it is not allowed.
        var ex = Assert.Throws<BusinessRuleException>(() =>
            claim.MoveTo(ClaimStatus.Approved, changedByUserId: 1, note: null, changedAt: Now));

        Assert.Equal("Can't move a claim from Submitted to Approved.", ex.Message);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Single(claim.History);   // no history row was written for the refused move
    }

    [Fact]
    public void MoveTo_StoresATrimmedNote_AndTurnsABlankNoteIntoNull()
    {
        var claim = Submit();

        claim.MoveTo(ClaimStatus.InReview, changedByUserId: 1, note: "  Looks complete  ", changedAt: Now);
        claim.MoveTo(ClaimStatus.InfoRequested, changedByUserId: 1, note: "   ", changedAt: Now);

        // History is oldest first: [filed, InReview, InfoRequested].
        var rows = claim.History.ToList();
        Assert.Equal("Looks complete", rows[1].Note);
        Assert.Null(rows[2].Note);
    }

    [Fact]
    public void MoveTo_WalksTheWholeApprovalPath_AndKeepsEveryStep()
    {
        var claim = Submit();

        claim.MoveTo(ClaimStatus.InReview, 1, null, Now);
        claim.MoveTo(ClaimStatus.Approved, 1, null, Now);
        claim.MoveTo(ClaimStatus.Closed, 1, null, Now);

        Assert.Equal(ClaimStatus.Closed, claim.Status);

        // The trail records every status the claim was ever in, in order.
        ClaimStatus[] expected =
            [ClaimStatus.Submitted, ClaimStatus.InReview, ClaimStatus.Approved, ClaimStatus.Closed];
        Assert.Equal(expected, claim.History.Select(h => h.ToStatus).ToArray());
    }

    [Fact]
    public void MoveTo_Throws_OnceTheClaimIsClosed()
    {
        var claim = Submit();
        claim.MoveTo(ClaimStatus.InReview, 1, null, Now);
        claim.MoveTo(ClaimStatus.Denied, 1, null, Now);
        claim.MoveTo(ClaimStatus.Closed, 1, null, Now);

        Assert.Throws<BusinessRuleException>(() => claim.MoveTo(ClaimStatus.InReview, 1, null, Now));
    }
}
