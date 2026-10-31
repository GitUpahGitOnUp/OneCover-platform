using System.Net;
using System.Net.Http.Json;
using OneCover.Disbursements.Api.Dtos;
using OneCover.Disbursements.Api.Models;
using OneCover.Disbursements.Api.Models.Enums;
using OneCover.Disbursements.Api.Repositories;
using OneCover.Disbursements.Api.Services;

namespace OneCover.Disbursements.Tests;

/// <summary>Tests for the admin dashboard summary: the adding-up in the service and the endpoint's access rules.</summary>
public class SummaryTests
{
    // A stand-in repository that returns whatever totals the test gives it. Only the summary method is used here.
    private class FakeRepository(List<StatusTotal> totals) : IDisbursementRepository
    {
        public Task<List<StatusTotal>> GetTotalsByStatusAsync() => Task.FromResult(totals);

        public Task<Disbursement?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task<Disbursement?> GetByIdForMemberAsync(int id, int memberId) => throw new NotImplementedException();
        public Task<Disbursement?> GetByClaimIdAsync(int claimId) => throw new NotImplementedException();
        public Task<List<Disbursement>> ListForMemberAsync(int memberId, int skip = 0, int take = 50) => throw new NotImplementedException();
        public Task<List<Disbursement>> ListAllAsync(DisbursementStatus? status = null, int skip = 0, int take = 50) => throw new NotImplementedException();
        public Task<bool> TryAddAsync(Disbursement disbursement) => throw new NotImplementedException();
        public Task SaveChangesAsync() => throw new NotImplementedException();
    }

    [Fact]
    public async Task TheSummary_AddsUpEveryStatus()
    {
        var service = new DisbursementService(new FakeRepository(
        [
            new StatusTotal(DisbursementStatus.Pending, 2, 300.50m),
            new StatusTotal(DisbursementStatus.Paid, 5, 1000m),
            new StatusTotal(DisbursementStatus.Failed, 1, 75m)
        ]), TimeProvider.System);

        var summary = await service.GetSummaryAsync();

        Assert.Equal(8, summary.TotalCount);
        Assert.Equal(2, summary.PendingCount);
        Assert.Equal(5, summary.PaidCount);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(300.50m, summary.PendingAmount);
        Assert.Equal(1000m, summary.PaidAmount);
        Assert.Equal(75m, summary.FailedAmount);
    }

    [Fact]
    public async Task AStatusWithNoRows_CountsAsZero()
    {
        // The database returns no row at all for a status nobody has, so the service must fill in zero.
        var service = new DisbursementService(new FakeRepository(
            [new StatusTotal(DisbursementStatus.Paid, 3, 90m)]), TimeProvider.System);

        var summary = await service.GetSummaryAsync();

        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(0, summary.PendingCount);
        Assert.Equal(0m, summary.PendingAmount);
        Assert.Equal(0, summary.FailedCount);
        Assert.Equal(0m, summary.FailedAmount);
    }

    [Fact]
    public async Task NoDisbursementsAtAll_GivesAllZeros()
    {
        var service = new DisbursementService(new FakeRepository([]), TimeProvider.System);

        var summary = await service.GetSummaryAsync();

        Assert.Equal(new DisbursementSummaryDto(0, 0, 0, 0, 0m, 0m, 0m), summary);
    }

    [Fact]
    public async Task AnAdmin_GetsTheSummary_NotAnIdLookup()
    {
        using var factory = new DisbursementsApiFactory().WithService(new FakeDisbursementService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 1, role: "Admin");

        var response = await client.GetAsync("/api/admin/disbursements/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<DisbursementSummaryDto>();
        Assert.Equal(3, summary?.TotalCount);
        Assert.Equal(200m, summary?.PaidAmount);
    }

    [Fact]
    public async Task AMember_CannotSeeTheSummary()
    {
        using var factory = new DisbursementsApiFactory().WithService(new FakeDisbursementService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 1, role: "Member");

        var response = await client.GetAsync("/api/admin/disbursements/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        using var factory = new DisbursementsApiFactory().WithService(new FakeDisbursementService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/disbursements/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
