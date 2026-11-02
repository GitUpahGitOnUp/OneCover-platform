using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OneCover.Claims.Api.Clients;
using OneCover.Claims.Api.Data;
using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Errors;
using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Models.Enums;
using OneCover.Claims.Api.Repositories;
using OneCover.Claims.Api.Services;
using OneCover.Contracts.Events;

namespace OneCover.Claims.Tests;

/// <summary>
/// Tests for the admin workflow. The service runs for real on an in-memory database; only the clock and the
/// event publisher (which would call Disbursements) are faked.
/// </summary>
public class AdminClaimServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Records the events it is asked to send, and can pretend delivery failed.
    private class FakePublisher(bool succeeds = true) : IClaimEventPublisher
    {
        public List<ClaimApproved> Published { get; } = [];

        public Task<bool> PublishClaimApprovedAsync(ClaimApproved claimApproved)
        {
            Published.Add(claimApproved);
            return Task.FromResult(succeeds);
        }
    }

    // Behaves like the real repository, except saving always fails as if another admin had changed the claim first.
    private class ConflictingRepository(IClaimRepository inner) : IClaimRepository
    {
        public Task<BenefitClaim?> GetByIdAsync(int id) => inner.GetByIdAsync(id);
        public Task<BenefitClaim?> GetByIdForMemberAsync(int id, int memberId) => inner.GetByIdForMemberAsync(id, memberId);
        public Task<List<BenefitClaim>> ListForMemberAsync(int memberId, int skip = 0, int take = 50) => inner.ListForMemberAsync(memberId, skip, take);
        public Task<List<BenefitClaim>> ListAllAsync(ClaimStatus? status = null, int skip = 0, int take = 50, bool awaitingPayoutOnly = false) => inner.ListAllAsync(status, skip, take, awaitingPayoutOnly);
        public Task<Dictionary<ClaimStatus, int>> CountByStatusAsync() => inner.CountByStatusAsync();
        public Task<int> CountAwaitingPayoutAsync() => inner.CountAwaitingPayoutAsync();
        public Task AddAsync(BenefitClaim claim) => inner.AddAsync(claim);
        public Task SaveChangesAsync() => throw new DbUpdateConcurrencyException("someone else got there first");
    }

    private static ClaimsDbContext OpenDb(string name) =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseInMemoryDatabase(name).Options);

    private static AdminClaimService NewService(string db, FakePublisher publisher, bool conflict = false, DateTimeOffset? now = null)
    {
        IClaimRepository repository = new ClaimRepository(OpenDb(db));
        if (conflict)
        {
            repository = new ConflictingRepository(repository);
        }

        return new AdminClaimService(repository, publisher, new FixedClock(now ?? Now), NullLogger<AdminClaimService>.Instance);
    }

    // Files a claim straight into the database. totalBenefit 0 models a leave that is all elimination days.
    private static async Task<int> FileClaim(string db, int memberId = 42, decimal totalBenefit = 900m)
    {
        var claim = BenefitClaim.Submit(
            memberId: memberId, enrollmentId: 7, coverageStartDate: new DateOnly(2026, 1, 1),
            planBenefitPercent: 0.60m, planWeeklyMaximum: 1_500m, planEliminationDays: 7,
            reason: "Surgery", startDate: new DateOnly(2026, 10, 1), endDate: new DateOnly(2026, 10, 14),
            weeklyBenefit: 900m, totalBenefit: totalBenefit, submittedAt: Now.AddDays(-5));
        await new ClaimRepository(OpenDb(db)).AddAsync(claim);
        return claim.Id;
    }

    private static UpdateClaimStatusRequest To(ClaimStatus status, string? note = null) => new(status, note);

    [Fact]
    public async Task MovingAClaim_RecordsTheAdminAndTheNote_InTheHistory()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db);
        var service = NewService(db, new FakePublisher());

        var claim = await service.UpdateStatusAsync(id, adminId: 900, To(ClaimStatus.InReview, "Starting review"));

        Assert.Equal(ClaimStatus.InReview, claim.Status);
        var last = claim.History.Last();
        Assert.Equal(ClaimStatus.Submitted, last.FromStatus);
        Assert.Equal(ClaimStatus.InReview, last.ToStatus);
        Assert.Equal(900, last.ChangedByUserId);
        Assert.Equal("Starting review", last.Note);
        Assert.Equal(Now, last.ChangedAt);
    }

    [Fact]
    public async Task TheMove_IsSaved()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db);

        await NewService(db, new FakePublisher()).UpdateStatusAsync(id, 900, To(ClaimStatus.InReview));

        var saved = await new ClaimRepository(OpenDb(db)).GetByIdAsync(id);
        Assert.Equal(ClaimStatus.InReview, saved!.Status);
        Assert.Equal(2, saved.History.Count);
    }

    [Fact]
    public async Task AMoveTheWorkflowForbids_IsRefused_AndChangesNothing()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db);
        var publisher = new FakePublisher();

        // Submitted -> Approved skips the review step, which the workflow does not allow.
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewService(db, publisher).UpdateStatusAsync(id, 900, To(ClaimStatus.Approved)));

        var saved = await new ClaimRepository(OpenDb(db)).GetByIdAsync(id);
        Assert.Equal(ClaimStatus.Submitted, saved!.Status);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task TheMemberWhoFiledAClaim_CannotApproveIt_AndNothingIsSent()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db, memberId: 42);
        var publisher = new FakePublisher();
        var service = NewService(db, publisher);
        await service.UpdateStatusAsync(id, 900, To(ClaimStatus.InReview));

        // The caller's id (42, from the JWT) is the claim's own member, so the approval is refused with a 409.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateStatusAsync(id, 42, To(ClaimStatus.Approved)));

        var saved = await new ClaimRepository(OpenDb(db)).GetByIdAsync(id);
        Assert.Equal(ClaimStatus.InReview, saved!.Status);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task ApprovingAClaim_SendsClaimApproved_WithTheClaimsOwnFigures()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db, memberId: 42, totalBenefit: 900m);
        var publisher = new FakePublisher();
        var service = NewService(db, publisher);
        await service.UpdateStatusAsync(id, 900, To(ClaimStatus.InReview));

        await service.UpdateStatusAsync(id, 900, To(ClaimStatus.Approved));

        // Exactly one event, built from the claim (the admin cannot choose an amount or a member).
        var sent = Assert.Single(publisher.Published);
        Assert.Equal(new ClaimApproved(ClaimId: id, MemberId: 42, Amount: 900m, ApprovedOn: new DateOnly(2026, 10, 20)), sent);
    }

    [Fact]
    public async Task OtherMoves_SendNothing()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db);
        var publisher = new FakePublisher();
        var service = NewService(db, publisher);

        await service.UpdateStatusAsync(id, 900, To(ClaimStatus.InReview));
        await service.UpdateStatusAsync(id, 900, To(ClaimStatus.Denied));

        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task IfDeliveryFails_TheClaimIsStillApproved_AndTheAdminStillGetsSuccess()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db);
        var service = NewService(db, new FakePublisher(succeeds: false));
        await service.UpdateStatusAsync(id, 900, To(ClaimStatus.InReview));

        var claim = await service.UpdateStatusAsync(id, 900, To(ClaimStatus.Approved));

        Assert.Equal(ClaimStatus.Approved, claim.Status);
        var saved = await new ClaimRepository(OpenDb(db)).GetByIdAsync(id);
        Assert.Equal(ClaimStatus.Approved, saved!.Status);
    }

    [Fact]
    public async Task AClaimWithNothingPayable_CannotBeApproved()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db, totalBenefit: 0m);
        var publisher = new FakePublisher();
        var service = NewService(db, publisher);
        await service.UpdateStatusAsync(id, 900, To(ClaimStatus.InReview));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateStatusAsync(id, 900, To(ClaimStatus.Approved)));

        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task AConcurrentChange_BecomesABusinessRuleError_AndSendsNothing()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db);
        var publisher = new FakePublisher();
        // First move normally to InReview, then try the approval with a repository that always loses the race.
        await NewService(db, publisher).UpdateStatusAsync(id, 900, To(ClaimStatus.InReview));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewService(db, publisher, conflict: true).UpdateStatusAsync(id, 900, To(ClaimStatus.Approved)));

        Assert.Contains("changed by someone else", error.Message);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task AMissingClaim_Is404()
    {
        var service = NewService(Guid.NewGuid().ToString(), new FakePublisher());

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateStatusAsync(12345, 900, To(ClaimStatus.InReview)));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(12345));
    }

    [Fact]
    public async Task ListAll_FiltersByStatus_AndShowsEveryMembersClaims()
    {
        var db = Guid.NewGuid().ToString();
        var first = await FileClaim(db, memberId: 1);
        await FileClaim(db, memberId: 2);
        var service = NewService(db, new FakePublisher());
        await service.UpdateStatusAsync(first, 900, To(ClaimStatus.InReview));

        var everything = await service.ListAllAsync(status: null, page: 1, pageSize: 50);
        var reviewing = await service.ListAllAsync(ClaimStatus.InReview, 1, 50);

        Assert.Equal(2, everything.Count);
        Assert.Equal([first], reviewing.Select(c => c.Id));
    }

    // ---- The payout alert: approved claims Disbursements has not confirmed ----

    // Files a claim, moves it to InReview, then approves it with the given publisher. Returns the claim id.
    private static async Task<int> ApproveWith(string db, FakePublisher publisher, int memberId = 42)
    {
        var id = await FileClaim(db, memberId);
        var service = NewService(db, publisher);
        await service.UpdateStatusAsync(id, 900, To(ClaimStatus.InReview));
        await service.UpdateStatusAsync(id, 900, To(ClaimStatus.Approved));
        return id;
    }

    [Fact]
    public async Task AnApprovalDisbursementsAccepted_IsNotFlagged_AndRecordsWhen()
    {
        var db = Guid.NewGuid().ToString();
        var id = await ApproveWith(db, new FakePublisher(succeeds: true));

        var saved = await new ClaimRepository(OpenDb(db)).GetByIdAsync(id);

        Assert.False(saved!.AwaitingPayout);
        Assert.Equal(Now, saved.PayoutRequestedAt);
    }

    [Fact]
    public async Task AnApprovalDisbursementsNeverReceived_IsFlaggedAsAwaitingPayout()
    {
        var db = Guid.NewGuid().ToString();
        var id = await ApproveWith(db, new FakePublisher(succeeds: false));

        var admin = await NewService(db, new FakePublisher()).GetAsync(id);

        Assert.Equal(ClaimStatus.Approved, admin.Status);
        Assert.True(admin.AwaitingPayout);
        Assert.Null(admin.PayoutRequestedAt);
    }

    [Fact]
    public async Task AClaimThatWasNeverApproved_IsNeverFlagged()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db);

        var claim = await NewService(db, new FakePublisher()).GetAsync(id);

        Assert.False(claim.AwaitingPayout);
    }

    [Fact]
    public async Task Resend_ClearsTheFlag_AndKeepsTheOriginalApprovalDate()
    {
        var db = Guid.NewGuid().ToString();
        var id = await ApproveWith(db, new FakePublisher(succeeds: false));   // approved on 2026-10-20, never delivered
        var publisher = new FakePublisher(succeeds: true);

        // Five days later an admin clicks "Re-send payout".
        var claim = await NewService(db, publisher, now: Now.AddDays(5)).ResendPayoutAsync(id);

        // The due date at Disbursements is worked out from ApprovedOn, so it must still be the REAL approval date.
        var sent = Assert.Single(publisher.Published);
        Assert.Equal(new DateOnly(2026, 10, 20), sent.ApprovedOn);
        Assert.False(claim.AwaitingPayout);
        Assert.Equal(Now.AddDays(5), claim.PayoutRequestedAt);
    }

    [Fact]
    public async Task Resend_WhenDisbursementsIsStillDown_Fails503_AndLeavesTheFlagOn()
    {
        var db = Guid.NewGuid().ToString();
        var id = await ApproveWith(db, new FakePublisher(succeeds: false));

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            NewService(db, new FakePublisher(succeeds: false)).ResendPayoutAsync(id));

        var saved = await new ClaimRepository(OpenDb(db)).GetByIdAsync(id);
        Assert.True(saved!.AwaitingPayout);
    }

    [Fact]
    public async Task Resend_IsRefused_WhenThePayoutWasAlreadyConfirmed_WithoutBotheringDisbursements()
    {
        var db = Guid.NewGuid().ToString();
        var id = await ApproveWith(db, new FakePublisher(succeeds: true));
        var publisher = new FakePublisher();

        await Assert.ThrowsAsync<BusinessRuleException>(() => NewService(db, publisher).ResendPayoutAsync(id));

        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task Resend_IsRefused_ForAClaimThatWasNeverApproved()
    {
        var db = Guid.NewGuid().ToString();
        var id = await FileClaim(db);
        var publisher = new FakePublisher();

        await Assert.ThrowsAsync<BusinessRuleException>(() => NewService(db, publisher).ResendPayoutAsync(id));

        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task Resend_OfAMissingClaim_Is404()
    {
        var service = NewService(Guid.NewGuid().ToString(), new FakePublisher());

        await Assert.ThrowsAsync<NotFoundException>(() => service.ResendPayoutAsync(12345));
    }

    [Fact]
    public async Task AnApprovedClaimThatWasThenClosed_IsStillFlagged_AndCanStillBeResent()
    {
        var db = Guid.NewGuid().ToString();
        var id = await ApproveWith(db, new FakePublisher(succeeds: false));
        // Closing it changes Status to Closed, so Status alone would hide the problem. The history still shows it was approved.
        await NewService(db, new FakePublisher()).UpdateStatusAsync(id, 900, To(ClaimStatus.Closed));
        var publisher = new FakePublisher(succeeds: true);

        var flagged = await NewService(db, publisher).ListAllAsync(status: null, page: 1, pageSize: 50, awaitingPayoutOnly: true);
        var resent = await NewService(db, publisher).ResendPayoutAsync(id);

        Assert.Equal([id], flagged.Select(c => c.Id));
        Assert.Equal(ClaimStatus.Closed, flagged.Single().Status);
        Assert.False(resent.AwaitingPayout);
    }

    [Fact]
    public async Task TheAwaitingPayoutList_ShowsOnlyTheClaimsThatNeedAttention()
    {
        var db = Guid.NewGuid().ToString();
        var needsAttention = await ApproveWith(db, new FakePublisher(succeeds: false), memberId: 1);
        await ApproveWith(db, new FakePublisher(succeeds: true), memberId: 2);   // delivered, fine
        await FileClaim(db, memberId: 3);                                        // never approved

        var flagged = await NewService(db, new FakePublisher()).ListAllAsync(null, 1, 50, awaitingPayoutOnly: true);

        Assert.Equal([needsAttention], flagged.Select(c => c.Id));
    }

    [Fact]
    public async Task TheSummary_CountsEveryStatus_IncludingZeros_AndTheAwaitingPayoutWarning()
    {
        var db = Guid.NewGuid().ToString();
        await ApproveWith(db, new FakePublisher(succeeds: false), memberId: 1);   // Approved, flagged
        await ApproveWith(db, new FakePublisher(succeeds: true), memberId: 2);    // Approved, fine
        await FileClaim(db, memberId: 3);                                         // Submitted

        var summary = await NewService(db, new FakePublisher()).GetSummaryAsync();

        Assert.Equal(3, summary.TotalClaims);
        Assert.Equal(1, summary.AwaitingPayoutCount);
        // Every status is listed, in workflow order, even the ones nobody has.
        Assert.Equal(Enum.GetValues<ClaimStatus>(), summary.ClaimsByStatus.Select(x => x.Status));
        Assert.Equal(1, summary.ClaimsByStatus.Single(x => x.Status == ClaimStatus.Submitted).Count);
        Assert.Equal(2, summary.ClaimsByStatus.Single(x => x.Status == ClaimStatus.Approved).Count);
        Assert.Equal(0, summary.ClaimsByStatus.Single(x => x.Status == ClaimStatus.Denied).Count);
    }

    [Fact]
    public async Task TheSummary_OfAnEmptyDatabase_IsAllZeros()
    {
        var summary = await NewService(Guid.NewGuid().ToString(), new FakePublisher()).GetSummaryAsync();

        Assert.Equal(0, summary.TotalClaims);
        Assert.Equal(0, summary.AwaitingPayoutCount);
        Assert.All(summary.ClaimsByStatus, x => Assert.Equal(0, x.Count));
    }
}
