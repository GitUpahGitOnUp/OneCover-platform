using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace OneCover.Claims.Tests;

/// <summary>
/// Integration tests that call the real Claims pipeline (routing, JWT, roles, validation, error handler)
/// with a fake service behind it, and check the access rules and error format.
/// </summary>
public class ClaimsEndpointTests
{
    // A valid body for POST /api/claims. Note there are no amount fields and no member id.
    private static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

    private const string ValidClaim =
        """{ "enrollmentId": 7, "reason": "Surgery", "startDate": "2026-10-01", "endDate": "2026-10-14" }""";

    // Reads a response and asserts it is a ProblemDetails with the expected status.
    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expected, body.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task NoToken_Returns401_InTheProblemDetailsFormat()
    {
        using var factory = new ClaimsApiFactory().WithService(new FakeClaimService());
        using var client = factory.CreateClient();

        await AssertProblem(await client.GetAsync("/api/claims"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnAdmin_CannotUseTheMemberEndpoints()
    {
        using var factory = new ClaimsApiFactory().WithService(new FakeClaimService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 1, role: "Admin");

        await AssertProblem(await client.GetAsync("/api/claims"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AMemberOnlyEverAsksForTheirOwnClaims_TheIdComesFromTheToken()
    {
        var service = new FakeClaimService();
        using var factory = new ClaimsApiFactory().WithService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 7, role: "Member");

        // Whatever else the request says, the member id handed to the service is the one in the token.
        var response = await client.GetAsync("/api/claims?memberId=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, service.MemberIdSeen);
    }

    [Fact]
    public async Task FilingAClaim_Returns201_WithALocation_AndTheMemberComesFromTheToken()
    {
        var service = new FakeClaimService();
        using var factory = new ClaimsApiFactory().WithService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 7, role: "Member");

        // The body tries to sneak in a member id and amounts. Neither is part of the request, so both are ignored.
        var response = await client.PostAsync("/api/claims", Body(
            """{ "memberId": 999, "totalBenefit": 1000000, "enrollmentId": 7, "reason": "Surgery", "startDate": "2026-10-01", "endDate": "2026-10-14" }"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/claims/11", response.Headers.Location?.ToString());
        Assert.Equal(7, service.MemberIdSeen);
        Assert.Equal(7, service.RequestSeen?.EnrollmentId);
    }

    [Theory]
    [InlineData("""{ "enrollmentId": 0, "reason": "Surgery", "startDate": "2026-10-01", "endDate": "2026-10-14" }""")]   // not a real id
    [InlineData("""{ "enrollmentId": 7, "reason": "", "startDate": "2026-10-01", "endDate": "2026-10-14" }""")]          // blank reason
    [InlineData("""{ "enrollmentId": 7, "startDate": "2026-10-01", "endDate": "2026-10-14" }""")]                         // no reason at all
    [InlineData("""{ "enrollmentId": 7, "reason": "Surgery", "startDate": "not-a-date", "endDate": "2026-10-14" }""")]  // unreadable date
    public async Task ABadClaimBody_Returns400_AndNeverReachesTheService(string json)
    {
        var service = new FakeClaimService();
        using var factory = new ClaimsApiFactory().WithService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 7, role: "Member");

        var response = await client.PostAsync("/api/claims", Body(json));

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Null(service.RequestSeen);
    }

    [Fact]
    public async Task ATooLongReason_Returns400()
    {
        using var factory = new ClaimsApiFactory().WithService(new FakeClaimService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 7, role: "Member");
        var reason = new string('x', 1001);

        var response = await client.PostAsJsonAsync("/api/claims",
            new { enrollmentId = 7, reason, startDate = "2026-10-01", endDate = "2026-10-14" });

        await AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AClaimThatIsNotFound_Returns404_WithTheFixedMessage()
    {
        using var factory = new ClaimsApiFactory().WithService(new FakeClaimService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 7, role: "Member");

        var response = await client.GetAsync("/api/claims/99");

        await AssertProblem(response, HttpStatusCode.NotFound);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Claim 99 was not found.", body.RootElement.GetProperty("detail").GetString());
    }
}
