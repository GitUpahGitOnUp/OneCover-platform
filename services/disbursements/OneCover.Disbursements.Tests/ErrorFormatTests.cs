using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OneCover.Disbursements.Api.Models.Enums;

namespace OneCover.Disbursements.Tests;

/// <summary>
/// Integration tests that call the real API pipeline and check that every kind of error comes back
/// in the same ProblemDetails format, and that members only ever reach their own data.
/// </summary>
public class ErrorFormatTests
{
    // Reads a response and asserts it is a ProblemDetails with the expected status. Returns the JSON for more checks.
    private static async Task<JsonElement> ReadProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expected, body.RootElement.GetProperty("status").GetInt32());
        return body.RootElement.Clone();
    }

    [Fact]
    public async Task NoToken_Returns401_InTheProblemDetailsFormat()
    {
        // A missing token is not an exception, so only UseStatusCodePages can give this response a body.
        using var factory = new DisbursementsApiFactory().WithService(new FakeDisbursementService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/disbursements");

        await ReadProblem(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenSignedWithTheWrongKey_Returns401()
    {
        using var factory = new DisbursementsApiFactory().WithService(new FakeDisbursementService());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            TestTokens.Create(userId: 1, role: "Member", key: "a-different-key-that-is-also-32-bytes-long!!"));

        var response = await client.GetAsync("/api/disbursements");

        await ReadProblem(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AMemberCallingAnAdminEndpoint_Returns403_InTheProblemDetailsFormat()
    {
        using var factory = new DisbursementsApiFactory().WithService(new FakeDisbursementService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 1, role: "Member");

        var response = await client.GetAsync("/api/admin/disbursements");

        await ReadProblem(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnUnknownUrl_Returns404_InTheProblemDetailsFormat()
    {
        using var factory = new DisbursementsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/nothing-here");

        await ReadProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ARecordThatIsNotFound_Returns404_WithTheFixedMessage()
    {
        using var factory = new DisbursementsApiFactory().WithService(new FakeDisbursementService());
        using var client = factory.CreateClient();
        client.Authenticate(userId: 7, role: "Member");

        var response = await client.GetAsync("/api/disbursements/99");

        var problem = await ReadProblem(response, HttpStatusCode.NotFound);
        Assert.Equal("Disbursement 99 was not found.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task AMemberOnlyEverAsksForTheirOwnData_TheIdComesFromTheToken()
    {
        var service = new FakeDisbursementService();
        using var factory = new DisbursementsApiFactory().WithService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 7, role: "Member");

        // Whatever else the request says, the member id handed to the service is the one in the token.
        var response = await client.GetAsync("/api/disbursements?memberId=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, service.MemberIdSeen);
    }

    [Fact]
    public async Task AStatusSentAsAWord_IsAccepted()
    {
        var service = new FakeDisbursementService();
        using var factory = new DisbursementsApiFactory().WithService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 1, role: "Admin");

        var response = await client.PutAsJsonAsync("/api/admin/disbursements/5/status", new { status = "Paid" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DisbursementStatus.Paid, service.StatusRequestSeen?.Status);
    }

    // [Theory] runs the same test once per row. Each row is a number where a status word is expected.
    [Theory]
    [InlineData("1")]    // a real status value, but as a number
    [InlineData("99")]   // not a status at all
    public async Task AStatusSentAsANumber_IsRefusedWith400(string status)
    {
        var service = new FakeDisbursementService();
        using var factory = new DisbursementsApiFactory().WithService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 1, role: "Admin");

        var body = new StringContent($$"""{ "status": {{status}} }""", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PutAsync("/api/admin/disbursements/5/status", body);

        await ReadProblem(response, HttpStatusCode.BadRequest);
        Assert.Null(service.StatusRequestSeen);   // the service was never reached
    }
}
