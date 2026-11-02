using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneCover.Claims.Api.Clients;
using OneCover.Contracts.Events;

namespace OneCover.Claims.Tests;

/// <summary>Tests for the HTTP publisher: what it sends, and when it retries, gives up or succeeds.</summary>
public class HttpClaimEventPublisherTests
{
    private static readonly ClaimApproved Event = new(ClaimId: 11, MemberId: 42, Amount: 900.50m, ApprovedOn: new DateOnly(2026, 10, 20));

    // A fake "network": answers each request from a list of prepared answers (a status code, or an exception to throw)
    // and remembers every request it received. No real HTTP happens.
    private class ScriptedHandler(params object[] answers) : HttpMessageHandler
    {
        private int _next;
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            var answer = answers[Math.Min(_next++, answers.Length - 1)];
            return answer switch
            {
                HttpStatusCode code => new HttpResponseMessage(code),
                Exception ex => throw ex,
                _ => throw new InvalidOperationException("Bad script")
            };
        }
    }

    private static HttpClaimEventPublisher NewPublisher(ScriptedHandler handler, int maxAttempts = 3)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://disbursements.test/") };
        http.DefaultRequestHeaders.Add("X-Internal-Api-Key", "secret-key");
        // RetryDelay zero = no real waiting, so the tests run instantly.
        var options = Options.Create(new DisbursementsOptions { MaxAttempts = maxAttempts, RetryDelay = TimeSpan.Zero });
        return new HttpClaimEventPublisher(http, options, NullLogger<HttpClaimEventPublisher>.Instance);
    }

    [Fact]
    public async Task ItPostsTheEventAsJson_ToTheAgreedPath_WithTheInternalKey()
    {
        var handler = new ScriptedHandler(HttpStatusCode.NoContent);

        var delivered = await NewPublisher(handler).PublishClaimApprovedAsync(Event);

        Assert.True(delivered);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://disbursements.test/internal/events/claim-approved", request.RequestUri?.ToString());
        Assert.Equal("secret-key", request.Headers.GetValues("X-Internal-Api-Key").Single());

        // The JSON matches the shared ClaimApproved contract, with the date as yyyy-MM-dd.
        using var json = JsonDocument.Parse(body);
        Assert.Equal(11, json.RootElement.GetProperty("claimId").GetInt32());
        Assert.Equal(42, json.RootElement.GetProperty("memberId").GetInt32());
        Assert.Equal(900.50m, json.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("2026-10-20", json.RootElement.GetProperty("approvedOn").GetString());
    }

    [Fact]
    public async Task ATemporaryFailure_IsRetried_UntilItWorks()
    {
        // Down (500), unreachable, then fine: delivered on the third try.
        var handler = new ScriptedHandler(HttpStatusCode.InternalServerError, new HttpRequestException("refused"), HttpStatusCode.NoContent);

        var delivered = await NewPublisher(handler).PublishClaimApprovedAsync(Event);

        Assert.True(delivered);
        Assert.Equal(3, handler.Calls.Count);
    }

    [Fact]
    public async Task ATimeout_IsRetried()
    {
        var handler = new ScriptedHandler(new TaskCanceledException("timed out"), HttpStatusCode.NoContent);

        Assert.True(await NewPublisher(handler).PublishClaimApprovedAsync(Event));
        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task ItGivesUpAfterTheMaximumAttempts_AndReturnsFalseWithoutThrowing()
    {
        var handler = new ScriptedHandler(HttpStatusCode.ServiceUnavailable);

        var delivered = await NewPublisher(handler, maxAttempts: 3).PublishClaimApprovedAsync(Event);

        Assert.False(delivered);
        Assert.Equal(3, handler.Calls.Count);
    }

    // [Theory] runs the same test once per row. Each row is an answer that will not improve by asking again.
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]   // wrong internal key
    [InlineData(HttpStatusCode.BadRequest)]     // bad data
    [InlineData(HttpStatusCode.Conflict)]       // Disbursements refused it (for example a zero amount)
    public async Task APermanentRefusal_IsNotRetried(HttpStatusCode status)
    {
        var handler = new ScriptedHandler(status);

        var delivered = await NewPublisher(handler).PublishClaimApprovedAsync(Event);

        Assert.False(delivered);
        Assert.Single(handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task SlowDownAnswers_AreRetried(HttpStatusCode status)
    {
        var handler = new ScriptedHandler(status, HttpStatusCode.NoContent);

        Assert.True(await NewPublisher(handler).PublishClaimApprovedAsync(Event));
        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task AMaxAttemptsOfZero_StillTriesOnce()
    {
        var handler = new ScriptedHandler(HttpStatusCode.NoContent);

        Assert.True(await NewPublisher(handler, maxAttempts: 0).PublishClaimApprovedAsync(Event));
        Assert.Single(handler.Calls);
    }
}
