using Microsoft.EntityFrameworkCore;
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
        public Task<List<BenefitClaim>> ListAllAsync(ClaimStatus? status = null, int skip = 0, int take = 50) => inner.ListAllAsync(status, skip, take);
        public Task AddAsync(BenefitClaim claim) => inner.AddAsync(claim);
        public Task SaveChangesAsync() => throw new DbUpdateConcurrencyException("someone else got there first");
    }

    private static ClaimsDbContext OpenDb(string name) =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseInMemoryDatabase(name).Options);

    private static AdminClaimService NewService(string db, FakePublisher publisher, bool conflict = false)
    {
        IClaimRepository repository = new ClaimRepository(OpenDb(db));
        if (conflict)
        {
            repository = new ConflictingRepository(repository);
        }

        return new AdminClaimService(repository, publisher, new FixedClock(Now));
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
}
