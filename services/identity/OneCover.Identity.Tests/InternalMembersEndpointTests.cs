using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneCover.Identity.Api.Dtos;
using OneCover.Identity.Api.Models;
using OneCover.Identity.Api.Services;

namespace OneCover.Identity.Tests;

public sealed class InternalMembersEndpointTests
{
    private const string ApiKey = "test-internal-key";

    [Fact]
    public async Task GetMember_ReturnsOnlyTheDocumentedInternalFields()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", ApiKey);

        var response = await client.GetAsync("/internal/members/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ["id", "role", "firstName", "lastName", "hireDate", "annualSalary", "employmentType"],
            body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(42, body.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(78_000m, body.RootElement.GetProperty("annualSalary").GetDecimal());
        Assert.False(body.RootElement.TryGetProperty("passwordHash", out _));
        Assert.False(body.RootElement.TryGetProperty("ssnLast4", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task GetMember_RequiresTheInternalKey(string? providedKey)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        if (providedKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", providedKey);
        }

        var response = await client.GetAsync("/internal/members/42");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetMember_Returns404ForAnUnknownMember()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", ApiKey);

        var response = await client.GetAsync("/internal/members/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
    }

    [Theory]
    [InlineData("Jwt:Key")]
    [InlineData("InternalApiKey")]
    [InlineData("DemoAdmin:Password")]
    public void Startup_RejectsPlaceholderSecrets(string settingName)
    {
        using var factory = new TestFactory(new FakeProfileService(), settingName);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains($"Setting '{settingName}'", exception.ToString());
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var service = new FakeProfileService();
        return new TestFactory(service);
    }

    private sealed class TestFactory(
        IProfileService profileService,
        string? placeholderSetting = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:IdentityDb",
                "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True");
            builder.UseSetting("Jwt:Key", "test-only-key-that-is-at-least-32-bytes-long!");
            builder.UseSetting("Jwt:Issuer", "test-issuer");
            builder.UseSetting("Jwt:Audience", "test-audience");
            builder.UseSetting("InternalApiKey", ApiKey);
            if (placeholderSetting is not null)
            {
                builder.UseSetting(placeholderSetting, "REPLACE_test-placeholder");
            }
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IProfileService>();
                services.AddSingleton(profileService);
            });
        }
    }

    private sealed class FakeProfileService : IProfileService
    {
        public Task<InternalMemberResponse> GetInternalMemberAsync(
            int id,
            CancellationToken cancellationToken)
        {
            if (id != 42)
            {
                throw new KeyNotFoundException("Member was not found.");
            }

            return Task.FromResult(new InternalMemberResponse(
                Id: id,
                Role: "Member",
                FirstName: "Casey",
                LastName: "Member",
                HireDate: new DateOnly(2020, 1, 15),
                AnnualSalary: 78_000m,
                EmploymentType: EmploymentType.FullTime));
        }

        public Task<ProfileResponse> GetMineAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileResponse> UpdateMineAsync(
            UpdateProfileRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
