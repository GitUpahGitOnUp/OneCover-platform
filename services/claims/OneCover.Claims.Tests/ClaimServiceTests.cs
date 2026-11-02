using Microsoft.EntityFrameworkCore;
using OneCover.Claims.Api.Clients;
using OneCover.Claims.Api.Data;
using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Errors;
using OneCover.Claims.Api.Models.Enums;
using OneCover.Claims.Api.Repositories;
using OneCover.Claims.Api.Services;

namespace OneCover.Claims.Tests;

/// <summary>
/// Tests for filing and reading claims. The service runs for real, with the real repository on an in-memory
/// database and the real calculator. Only the other services (Enrollment, Identity) and the clock are faked.
/// </summary>
public class ClaimServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    // A clock that always says the same time, so SubmittedAt can be checked exactly.
    private class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Returns whatever enrollment the test gives it (or null for "no such enrollment").
    private class FakeEnrollmentClient(EnrollmentTerms? terms) : IEnrollmentClient
    {
        public Task<EnrollmentTerms?> GetEnrollmentAsync(
            int enrollmentId,
            CancellationToken cancellationToken = default) => Task.FromResult(terms);
    }

    // Returns the given salary for any member, or null for "Identity doesn't know this member".
    private class FakeIdentityClient(decimal? salary) : IIdentityClient
    {
        public Task<MemberInfo?> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) =>
            Task.FromResult(salary is null ? null : new MemberInfo(memberId, salary.Value));
    }

    // An STD enrollment owned by member 42: 60% of salary, capped at 1,500 a week, 7 unpaid days, covered since 2026-01-01.
    private static EnrollmentTerms Std(int memberId = 42, bool isActive = true, string planType = "STD") =>
        new(Id: 7, MemberId: memberId, PlanType: planType, IsActive: isActive,
            CoverageStartDate: new DateOnly(2026, 1, 1), BenefitPercent: 0.60m, WeeklyMaximum: 1_500m, EliminationDays: 7);

    private static SubmitClaimRequest Request(
        string reason = "Recovering from surgery", int enrollmentId = 7,
        DateOnly? start = null, DateOnly? end = null) =>
        new(EnrollmentId: enrollmentId, Reason: reason,
            StartDate: start ?? new DateOnly(2026, 10, 1), EndDate: end ?? new DateOnly(2026, 10, 14));

    // Builds the service and gives back its database name so a test can open a second context to inspect what was saved.
    private static (ClaimService Service, string Db) NewService(EnrollmentTerms? enrollment, decimal? salary = 78_000m)
    {
        var db = Guid.NewGuid().ToString();
        return (NewService(db, enrollment, salary), db);
    }

    private static ClaimService NewService(string db, EnrollmentTerms? enrollment, decimal? salary = 78_000m) =>
        new(new ClaimRepository(OpenDb(db)),
            new FakeEnrollmentClient(enrollment),
            new FakeIdentityClient(salary),
            new BenefitCalculator(),
            new FixedClock(Now));

    private static ClaimsDbContext OpenDb(string name) =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task Submit_CalculatesTheBenefitOnTheServer_AndSavesTheClaim()
    {
        var (service, db) = NewService(Std());

        var claim = await service.SubmitAsync(memberId: 42, Request());

        // 78,000 / 52 = 1,500 a week x 60% = 900. Leave is 14 days, the first 7 are unpaid: 900 / 7 x 7 = 900.
        Assert.Equal(900m, claim.WeeklyBenefit);
        Assert.Equal(900m, claim.TotalBenefit);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal(42, claim.MemberId);
        Assert.Equal(Now, claim.SubmittedAt);   // the time came from TimeProvider

        // The claim really was saved: a fresh context finds it, with its first history row.
        var saved = await new ClaimRepository(OpenDb(db)).GetByIdAsync(claim.Id);
        Assert.NotNull(saved);
        Assert.Single(saved.History);
    }

    [Fact]
    public async Task Submit_KeepsThePlanTermsItWasFiledUnder()
    {
        var (service, _) = NewService(Std());

        var claim = await service.SubmitAsync(memberId: 42, Request());

        Assert.Equal(0.60m, claim.PlanBenefitPercent);
        Assert.Equal(1_500m, claim.PlanWeeklyMaximum);
        Assert.Equal(7, claim.PlanEliminationDays);
    }

    [Fact]
    public async Task Submit_CapsTheWeeklyBenefitAtThePlanMaximum()
    {
        // 260,000 / 52 = 5,000 a week x 60% = 3,000, but the plan pays at most 1,500.
        var (service, _) = NewService(Std(), salary: 260_000m);

        var claim = await service.SubmitAsync(memberId: 42, Request());

        Assert.Equal(1_500m, claim.WeeklyBenefit);
    }

    [Fact]
    public async Task Submit_Refuses_AnEnrollmentThatDoesNotExist()
    {
        var (service, _) = NewService(enrollment: null);

        var error = await Assert.ThrowsAsync<NotFoundException>(() => service.SubmitAsync(42, Request(enrollmentId: 99)));

        Assert.Equal("Enrollment 99 was not found.", error.Message);
    }

    [Fact]
    public async Task Submit_Refuses_SomeoneElsesEnrollment_WithTheSame404()
    {
        // The enrollment exists but belongs to member 5, not the member filing (42).
        var (service, _) = NewService(Std(memberId: 5));

        var error = await Assert.ThrowsAsync<NotFoundException>(() => service.SubmitAsync(42, Request(enrollmentId: 7)));

        // Identical wording to "doesn't exist", so the API never confirms the enrollment is there.
        Assert.Equal("Enrollment 7 was not found.", error.Message);
    }

    [Fact]
    public async Task Submit_Refuses_AnInactiveEnrollment()
    {
        var (service, _) = NewService(Std(isActive: false));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitAsync(42, Request()));
    }

    [Theory]
    [InlineData("Dental")]
    [InlineData("Life")]
    public async Task Submit_Refuses_APlanThatIsNotStdOrLtd(string planType)
    {
        var (service, _) = NewService(Std(planType: planType));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitAsync(42, Request()));
    }

    [Theory]
    [InlineData("STD")]
    [InlineData("LTD")]
    [InlineData("std")]
    public async Task Submit_Accepts_StdAndLtd_IgnoringCase(string planType)
    {
        var (service, _) = NewService(Std(planType: planType));

        var claim = await service.SubmitAsync(42, Request());

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
    }

    [Fact]
    public async Task Submit_Refuses_AnEndDateBeforeTheStartDate_AsABusinessRule_NotACrash()
    {
        var (service, _) = NewService(Std());

        // Without the check in the service, the calculator would throw ArgumentException (a 500).
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(42, Request(start: new DateOnly(2026, 10, 14), end: new DateOnly(2026, 10, 1))));
    }

    [Fact]
    public async Task Submit_Refuses_ALeaveThatStartsBeforeCoverageBegan()
    {
        var (service, _) = NewService(Std());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(42, Request(start: new DateOnly(2025, 12, 1), end: new DateOnly(2025, 12, 20))));
    }

    [Fact]
    public async Task Submit_Refuses_ALeaveLongerThanAYear()
    {
        var (service, _) = NewService(Std());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(42, Request(start: new DateOnly(2026, 3, 1), end: new DateOnly(2027, 3, 1))));
    }

    [Fact]
    public async Task Submit_Refuses_AMemberIdentityDoesNotKnow()
    {
        var (service, _) = NewService(Std(), salary: null);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SubmitAsync(42, Request()));
    }

    [Fact]
    public async Task Submit_SavesNothing_WhenARuleIsBroken()
    {
        var (service, db) = NewService(Std());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(42, Request(start: new DateOnly(2025, 12, 1), end: new DateOnly(2025, 12, 20))));

        Assert.Empty(await new ClaimRepository(OpenDb(db)).ListForMemberAsync(42));
    }

    [Fact]
    public async Task GetForMember_ReturnsTheMembersOwnClaim()
    {
        var (service, _) = NewService(Std());
        var filed = await service.SubmitAsync(42, Request());

        var claim = await service.GetForMemberAsync(filed.Id, memberId: 42);

        Assert.Equal(filed.Id, claim.Id);
    }

    [Fact]
    public async Task GetForMember_Returns404_ForSomeoneElsesClaim_WithTheSameMessageAsAMissingOne()
    {
        var (service, _) = NewService(Std());
        var filed = await service.SubmitAsync(42, Request());

        var someoneElses = await Assert.ThrowsAsync<NotFoundException>(() => service.GetForMemberAsync(filed.Id, memberId: 99));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => service.GetForMemberAsync(12345, memberId: 99));

        Assert.Equal($"Claim {filed.Id} was not found.", someoneElses.Message);
        Assert.Equal("Claim 12345 was not found.", missing.Message);
    }

    [Fact]
    public async Task ListForMember_ShowsOnlyThatMembersClaims()
    {
        var db = Guid.NewGuid().ToString();
        // Two members file claims against the same database.
        await NewService(db, Std(memberId: 42)).SubmitAsync(42, Request());
        await NewService(db, Std(memberId: 5)).SubmitAsync(5, Request());

        var claims = await NewService(db, Std()).ListForMemberAsync(memberId: 42, page: 1, pageSize: 50);

        Assert.Single(claims);
        Assert.Equal(42, claims.Single().MemberId);
    }

    [Theory]
    [InlineData(0, 0)]      // page 0 and size 0 fall back to page 1 of the default size
    [InlineData(-3, 1000)]  // a negative page acts like page 1, and a huge size is capped
    public async Task ListForMember_CopesWithSillyPaging(int page, int pageSize)
    {
        var (service, _) = NewService(Std());
        await service.SubmitAsync(42, Request());

        var claims = await service.ListForMemberAsync(42, page, pageSize);

        Assert.Single(claims);
    }
}
