using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneCover.Enrollment.Api.Dtos;
using OneCover.Enrollment.Api.Models;
using OneCover.Enrollment.Api.Services;

namespace OneCover.Enrollment.Tests;

public sealed class InternalEnrollmentsEndpointTests
{
    private const string ApiKey = "test-internal-key";

    [Fact]
    public async Task GetEnrollment_ReturnsTheDocumentedInternalContract()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", ApiKey);

        var response = await client.GetAsync("/internal/enrollments/7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ["id", "memberId", "planType", "isActive", "coverageStartDate", "benefitPercent", "weeklyMaximum", "eliminationDays"],
            body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("STD", body.RootElement.GetProperty("planType").GetString());
        Assert.True(body.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal("2026-01-15", body.RootElement.GetProperty("coverageStartDate").GetString());
        Assert.Equal(0.60m, body.RootElement.GetProperty("benefitPercent").GetDecimal());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task GetEnrollment_RequiresTheInternalKey(string? providedKey)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        if (providedKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", providedKey);
        }

        var response = await client.GetAsync("/internal/enrollments/7");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetEnrollment_Returns404ForAnUnknownEnrollment()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", ApiKey);

        var response = await client.GetAsync("/internal/enrollments/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new TestFactory(new FakeEnrollmentService());

    private sealed class TestFactory(IEnrollmentService enrollmentService) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:EnrollmentDb",
                "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True");
            builder.UseSetting("Jwt:Key", "test-only-key-that-is-at-least-32-bytes-long!");
            builder.UseSetting("Jwt:Issuer", "test-issuer");
            builder.UseSetting("Jwt:Audience", "test-audience");
            builder.UseSetting("InternalApiKey", ApiKey);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEnrollmentService>();
                services.AddSingleton(enrollmentService);
            });
        }
    }

    private sealed class FakeEnrollmentService : IEnrollmentService
    {
        public Task<InternalEnrollmentResponse> GetInternalByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            if (id != 7)
            {
                throw new KeyNotFoundException("Enrollment was not found.");
            }

            return Task.FromResult(new InternalEnrollmentResponse(
                Id: id,
                MemberId: 42,
                PlanType: "STD",
                IsActive: true,
                CoverageStartDate: new DateOnly(2026, 1, 15),
                BenefitPercent: 0.60m,
                WeeklyMaximum: 1_500m,
                EliminationDays: 7));
        }

        public Task<IReadOnlyList<EnrollmentResponse>> GetMineAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<EnrollmentResponse> GetMineByIdAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<EnrollmentResponse> EnrollAsync(
            EnrollRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<EnrollmentSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
