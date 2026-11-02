using Microsoft.EntityFrameworkCore;
using OneCover.Claims.Api.Data;
using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Models.Enums;
using OneCover.Claims.Api.Repositories;

namespace OneCover.Claims.Tests;

/// <summary>
/// Tests for ClaimRepository against EF Core's in-memory database, so they need no SQL Server.
/// (The in-memory provider checks our filtering, sorting and paging, but not SQL-specific things like rowversion.)
/// </summary>
public class ClaimRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    // Each test gets its OWN empty database (the name is a fresh Guid), so tests can't affect each other.
    private static ClaimsDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseInMemoryDatabase(name).Options);

    // Builds a valid claim for a member, filed "daysAgo" days before Now.
    private static BenefitClaim NewClaim(int memberId, int daysAgo = 0) =>
        BenefitClaim.Submit(
            memberId: memberId,
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
            submittedAt: Now.AddDays(-daysAgo));

    [Fact]
    public async Task AddedClaim_ComesBackWithItsFirstHistoryRow()
    {
        var name = Guid.NewGuid().ToString();
        int id;
        await using (var db = NewDb(name))
        {
            var claim = NewClaim(memberId: 42);
            await new ClaimRepository(db).AddAsync(claim);
            id = claim.Id;
        }

        // A second context = a fresh load from the "database", not the objects still in memory.
        await using var readDb = NewDb(name);
        var loaded = await new ClaimRepository(readDb).GetByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Single(loaded.History);
        Assert.Equal(ClaimStatus.Submitted, loaded.History.Single().ToStatus);
    }

    [Fact]
    public async Task GetByIdForMember_ReturnsNull_ForSomeoneElsesClaim()
    {
        await using var db = NewDb(Guid.NewGuid().ToString());
        var repository = new ClaimRepository(db);
        var claim = NewClaim(memberId: 42);
        await repository.AddAsync(claim);

        Assert.NotNull(await repository.GetByIdForMemberAsync(claim.Id, memberId: 42));
        Assert.Null(await repository.GetByIdForMemberAsync(claim.Id, memberId: 99));
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenTheClaimDoesNotExist()
    {
        await using var db = NewDb(Guid.NewGuid().ToString());

        Assert.Null(await new ClaimRepository(db).GetByIdAsync(12345));
    }

    [Fact]
    public async Task ListForMember_ReturnsOnlyThatMembersClaims_NewestFirst()
    {
        await using var db = NewDb(Guid.NewGuid().ToString());
        var repository = new ClaimRepository(db);
        var older = NewClaim(memberId: 42, daysAgo: 10);
        var newer = NewClaim(memberId: 42, daysAgo: 1);
        var someoneElses = NewClaim(memberId: 99);
        await repository.AddAsync(older);
        await repository.AddAsync(someoneElses);
        await repository.AddAsync(newer);

        var claims = await repository.ListForMemberAsync(memberId: 42);

        Assert.Equal([newer.Id, older.Id], claims.Select(c => c.Id));
    }

    [Fact]
    public async Task ListForMember_PagesTheResults()
    {
        await using var db = NewDb(Guid.NewGuid().ToString());
        var repository = new ClaimRepository(db);
        for (var i = 0; i < 5; i++)
        {
            await repository.AddAsync(NewClaim(memberId: 42, daysAgo: i));
        }

        var secondPage = await repository.ListForMemberAsync(memberId: 42, skip: 2, take: 2);

        // Newest first, so page 2 of size 2 holds the 3rd and 4th newest: filed 2 and 3 days ago.
        Assert.Equal(2, secondPage.Count);
        Assert.Equal([Now.AddDays(-2), Now.AddDays(-3)], secondPage.Select(c => c.SubmittedAt));
    }

    [Fact]
    public async Task ListAll_FiltersByStatus_AndWithoutAFilterReturnsEverything()
    {
        await using var db = NewDb(Guid.NewGuid().ToString());
        var repository = new ClaimRepository(db);
        var submitted = NewClaim(memberId: 1);
        var inReview = NewClaim(memberId: 2);
        inReview.MoveTo(ClaimStatus.InReview, changedByUserId: 900, note: null, changedAt: Now);
        await repository.AddAsync(submitted);
        await repository.AddAsync(inReview);

        var reviewing = await repository.ListAllAsync(status: ClaimStatus.InReview);
        var everything = await repository.ListAllAsync();

        Assert.Equal([inReview.Id], reviewing.Select(c => c.Id));
        Assert.Equal(2, everything.Count);
    }

    [Fact]
    public async Task TheCountsAndTheAwaitingPayoutFilter_AgreeWithEachOther()
    {
        await using var db = NewDb(Guid.NewGuid().ToString());
        var repository = new ClaimRepository(db);

        var flagged = NewClaim(memberId: 1);       // approved, payout never confirmed
        flagged.MoveTo(ClaimStatus.InReview, changedByUserId: 900, note: null, changedAt: Now);
        flagged.MoveTo(ClaimStatus.Approved, changedByUserId: 900, note: null, changedAt: Now);

        var confirmed = NewClaim(memberId: 2);     // approved, payout confirmed
        confirmed.MoveTo(ClaimStatus.InReview, changedByUserId: 900, note: null, changedAt: Now);
        confirmed.MoveTo(ClaimStatus.Approved, changedByUserId: 900, note: null, changedAt: Now);
        confirmed.MarkPayoutRequested(Now);

        var closed = NewClaim(memberId: 3);        // approved, then closed, payout never confirmed
        closed.MoveTo(ClaimStatus.InReview, changedByUserId: 900, note: null, changedAt: Now);
        closed.MoveTo(ClaimStatus.Approved, changedByUserId: 900, note: null, changedAt: Now);
        closed.MoveTo(ClaimStatus.Closed, changedByUserId: 900, note: null, changedAt: Now);

        var untouched = NewClaim(memberId: 4);     // never approved
        foreach (var claim in new[] { flagged, confirmed, closed, untouched })
        {
            await repository.AddAsync(claim);
        }

        var awaiting = await repository.ListAllAsync(awaitingPayoutOnly: true);
        var count = await repository.CountAwaitingPayoutAsync();
        var byStatus = await repository.CountByStatusAsync();

        Assert.Equal(2, count);
        Assert.Equal([closed.Id, flagged.Id], awaiting.Select(c => c.Id).OrderByDescending(id => id));
        Assert.Equal(1, byStatus[ClaimStatus.Submitted]);
        Assert.Equal(2, byStatus[ClaimStatus.Approved]);
        Assert.Equal(1, byStatus[ClaimStatus.Closed]);
        Assert.False(byStatus.ContainsKey(ClaimStatus.Denied));   // a status nobody has is simply absent
    }

    [Fact]
    public async Task AMoveSavedThroughTheRepository_AddsAHistoryRow()
    {
        var name = Guid.NewGuid().ToString();
        int id;
        await using (var db = NewDb(name))
        {
            var repository = new ClaimRepository(db);
            var claim = NewClaim(memberId: 42);
            await repository.AddAsync(claim);
            id = claim.Id;

            // Load it again (tracked), move it, and save. This is exactly what the admin status endpoint will do.
            var tracked = await repository.GetByIdAsync(id);
            tracked!.MoveTo(ClaimStatus.InReview, changedByUserId: 900, note: "Looking at it", changedAt: Now);
            await repository.SaveChangesAsync();
        }

        await using var readDb = NewDb(name);
        var loaded = await new ClaimRepository(readDb).GetByIdAsync(id);

        Assert.Equal(ClaimStatus.InReview, loaded!.Status);
        Assert.Equal(2, loaded.History.Count);
    }
}
