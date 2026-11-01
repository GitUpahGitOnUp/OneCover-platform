using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Tests;

/// <summary>Integration tests for the admin endpoints: who may call them, and what reaches the service.</summary>
public class AdminClaimsEndpointTests
{
    [Fact]
    public async Task AMember_CannotUseTheAdminEndpoints()
    {
        using var factory = new ClaimsApiFactory().WithAdminService(new FakeAdminClaimService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 1, role: "Member");

        var list = await client.GetAsync("/api/admin/claims");
        var move = await client.PutAsJsonAsync("/api/admin/claims/5/status", new { status = "InReview" });

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, move.StatusCode);
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        using var factory = new ClaimsApiFactory().WithAdminService(new FakeAdminClaimService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/claims");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheAdminWhoMovesAClaim_IsTheOneInTheToken_NotTheOneInTheBody()
    {
        var service = new FakeAdminClaimService();
        using var factory = new ClaimsApiFactory().WithAdminService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 900, role: "Admin");

        // The body tries to name a different admin. There is no such field, so it is ignored.
        var response = await client.PutAsJsonAsync("/api/admin/claims/5/status",
            new { status = "Approved", note = "ok", changedByUserId = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(900, service.AdminIdSeen);
        Assert.Equal(ClaimStatus.Approved, service.RequestSeen?.Status);
        Assert.Equal("ok", service.RequestSeen?.Note);
    }

    [Fact]
    public async Task TheStatusFilterIsPassedThrough()
    {
        var service = new FakeAdminClaimService();
        using var factory = new ClaimsApiFactory().WithAdminService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 900, role: "Admin");

        var response = await client.GetAsync("/api/admin/claims?status=InReview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ClaimStatus.InReview, service.FilterSeen);
    }

    // [Theory] runs the same test once per row. Each row is a bad status where a status word is expected.
    [Theory]
    [InlineData("1")]            // a real status value, but as a number
    [InlineData("99")]           // not a status at all
    [InlineData("\"Aproved\"")]  // a typo
    public async Task ABadStatus_Returns400_AndNeverReachesTheService(string status)
    {
        var service = new FakeAdminClaimService();
        using var factory = new ClaimsApiFactory().WithAdminService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 900, role: "Admin");

        var response = await client.PutAsync("/api/admin/claims/5/status",
            new StringContent($$"""{ "status": {{status}} }""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.RequestSeen);
    }

    [Fact]
    public async Task ATooLongNote_Returns400()
    {
        using var factory = new ClaimsApiFactory().WithAdminService(new FakeAdminClaimService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 900, role: "Admin");

        var response = await client.PutAsJsonAsync("/api/admin/claims/5/status",
            new { status = "InReview", note = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AClaimThatIsNotFound_Returns404()
    {
        using var factory = new ClaimsApiFactory().WithAdminService(new FakeAdminClaimService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 900, role: "Admin");

        var response = await client.GetAsync("/api/admin/claims/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Claim 99 was not found.", body.RootElement.GetProperty("detail").GetString());
    }
}
