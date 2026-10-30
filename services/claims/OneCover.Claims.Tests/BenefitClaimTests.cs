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
    private static BenefitClaim Submit(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string reason = "Recovering from surgery")
    {
        return BenefitClaim.Submit(
            memberId: 42,
            enrollmentId: 7,
            coverageStartDate: CoverageStart,
            planBenefitPercent: 0.60m,
            planWeeklyMaximum: 1_500m,
            planEliminationDays: 7,
            reason: reason,
            startDate: startDate ?? new DateOnly(2026, 10, 1),
            endDate: endDate ?? new DateOnly(2026, 10, 14),
            weeklyBenefit: 900m,
            totalBenefit: 900m,
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

        var ex = Assert.Throws<BusinessRuleException>(() =>
            Submit(startDate: start, endDate: start.AddYears(1).AddDays(1)));

        Assert.Equal("A single claim can't cover more than one year.", ex.Message);
    }

    [Fact]
    public void Submit_AllowsAClaimOfExactlyOneYear()
    {
        var start = new DateOnly(2026, 3, 1);

        var claim = Submit(startDate: start, endDate: start.AddYears(1));

        Assert.Equal(start.AddYears(1), claim.EndDate);
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
