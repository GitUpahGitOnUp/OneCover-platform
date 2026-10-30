using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OneCover.Disbursements.Tests;

/// <summary>
/// Integration tests for POST /internal/events/claim-approved: the shared-key check and the hand-off to the service.
/// Until now this was only checked by hand with curl.
/// </summary>
public class InternalEventsEndpointTests
{
    private const string Url = "/internal/events/claim-approved";

    // The event body Claims will send, as JSON. Property names are camelCase, which is ASP.NET's default.
    private static readonly object Event = new { claimId = 900001, memberId = 1, amount = 1500.50m, approvedOn = "2026-10-30" };

    // Sends the event, optionally with the key header.
    private static Task<HttpResponseMessage> Send(HttpClient client, string? key, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(body ?? Event) };
        if (key is not null)
        {
            request.Headers.Add("X-Internal-Api-Key", key);
        }

        return client.SendAsync(request);
    }

    [Fact]
    public async Task NoKey_Returns401_AndNeverReachesTheService()
    {
        var service = new FakeDisbursementService();
        using var factory = new DisbursementsApiFactory().WithService(service);
        using var client = factory.CreateClient();

        var response = await Send(client, key: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        Assert.Empty(service.Created);
    }

    [Fact]
    public async Task AWrongKey_Returns401_AndNeverReachesTheService()
    {
        var service = new FakeDisbursementService();
        using var factory = new DisbursementsApiFactory().WithService(service);
        using var client = factory.CreateClient();

        var response = await Send(client, key: "not-the-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(service.Created);
    }

    [Fact]
    public async Task AJwtIsNotEnough_ThePeerKeyIsWhatCounts()
    {
        // A service-to-service call is not a person, so a valid member or admin token must not open the endpoint.
        var service = new FakeDisbursementService();
        using var factory = new DisbursementsApiFactory().WithService(service);
        using var client = factory.CreateClient();
        client.Authenticate(userId: 1, role: "Admin");

        var response = await Send(client, key: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(service.Created);
    }

    [Fact]
    public async Task WhenTheKeyIsNotConfigured_NoKeyCanOpenTheEndpoint()
    {
        // A forgotten or blank InternalApiKey setting must close the endpoint, not leave it open.
        var service = new FakeDisbursementService();
        using var factory = new DisbursementsApiFactory().With("InternalApiKey", "").WithService(service);
        using var client = factory.CreateClient();

        var withEmptyKey = await Send(client, key: "");
        var withAnyKey = await Send(client, key: "anything");

        Assert.Equal(HttpStatusCode.Unauthorized, withEmptyKey.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withAnyKey.StatusCode);
        Assert.Empty(service.Created);
    }

    [Fact]
    public async Task TheRightKey_Returns204_AndHandsTheEventToTheService()
    {
        var service = new FakeDisbursementService();
        using var factory = new DisbursementsApiFactory().WithService(service);
        using var client = factory.CreateClient();

        var response = await Send(client, DisbursementsApiFactory.InternalKey);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var call = Assert.Single(service.Created);
        Assert.Equal(900001, call.ClaimId);
        Assert.Equal(1, call.MemberId);
        Assert.Equal(1500.50m, call.Amount);
        Assert.Equal(new DateOnly(2026, 10, 30), call.ApprovedOn);
    }

    [Fact]
    public async Task TheSameEventTwice_Returns204_BothTimes()
    {
        // The first delivery creates a row (true); the second finds it already there (false).
        // Both are success for the sender, so Claims never has to retry or special-case a duplicate.
        var service = new FakeDisbursementService();
        service.CreateResults.Enqueue(true);
        service.CreateResults.Enqueue(false);
        using var factory = new DisbursementsApiFactory().WithService(service);
        using var client = factory.CreateClient();

        var first = await Send(client, DisbursementsApiFactory.InternalKey);
        var second = await Send(client, DisbursementsApiFactory.InternalKey);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(2, service.Created.Count);
    }

    [Fact]
    public async Task AMalformedBody_Returns400_InTheProblemDetailsFormat()
    {
        var service = new FakeDisbursementService();
        using var factory = new DisbursementsApiFactory().WithService(service);
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new StringContent("{ this is not json", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Internal-Api-Key", DisbursementsApiFactory.InternalKey);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        Assert.Empty(service.Created);
    }
}
