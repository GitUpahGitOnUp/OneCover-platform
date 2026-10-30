using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OneCover.Disbursements.Api.Errors;

namespace OneCover.Disbursements.Tests;

/// <summary>Unit tests for the global exception handler: each exception type becomes the right ProblemDetails response.</summary>
public class GlobalExceptionHandlerTests
{
    // Runs the handler against a fake HTTP request and returns what it would send back.
    // DefaultHttpContext is ASP.NET's ready-made fake request/response. The response body is an in-memory
    // stream, so we can read the JSON the handler wrote. NullLogger is a logger that discards its messages.
    private static async Task<(int Status, string? ContentType, JsonElement Body, bool Handled)> Handle(
        Exception exception, DefaultHttpContext? context = null)
    {
        context ??= new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Rewind the stream to the start. If the handler wrote a body, read it; otherwise return an empty JSON value.
        context.Response.Body.Position = 0;
        var body = context.Response.Body.Length == 0
            ? default
            : (await JsonDocument.ParseAsync(context.Response.Body)).RootElement.Clone();

        return (context.Response.StatusCode, context.Response.ContentType, body, handled);
    }

    // Each test below throws one kind of exception at the handler and checks the status, title and detail.
    [Fact]
    public async Task NotFoundException_BecomesA404()
    {
        var result = await Handle(new NotFoundException("Disbursement", 5));

        Assert.True(result.Handled);
        Assert.Equal(404, result.Status);
        Assert.Equal("Not found", result.Body.GetProperty("title").GetString());
        Assert.Equal("Disbursement 5 was not found.", result.Body.GetProperty("detail").GetString());
    }

    [Fact]
    public void NotFoundException_HasOneFixedMessage_SoMissingAndNotYoursLookTheSame()
    {
        // The message is built from the resource and id only, so there is no way to word it differently
        // for "doesn't exist" and "belongs to someone else".
        Assert.Equal(new NotFoundException("Disbursement", 5).Message, new NotFoundException("Disbursement", 5).Message);
        Assert.Equal("Disbursement 5 was not found.", new NotFoundException("Disbursement", 5).Message);
    }

    [Fact]
    public async Task BusinessRuleException_BecomesA409()
    {
        var result = await Handle(new BusinessRuleException("Only pending disbursements can be marked paid. This one is Paid."));

        Assert.Equal(409, result.Status);
        Assert.Equal("Business rule violated", result.Body.GetProperty("title").GetString());
        Assert.Equal(
            "Only pending disbursements can be marked paid. This one is Paid.",
            result.Body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task UnauthorizedAccessException_BecomesA401_WithAFixedDetail()
    {
        // .NET itself throws UnauthorizedAccessException for things like file permissions, so its message
        // (which could name a path) must never be echoed to the client.
        var result = await Handle(new UnauthorizedAccessException(@"Access to the path 'C:\secret\file.txt' is denied."));

        Assert.Equal(401, result.Status);
        Assert.Equal("Unauthorized", result.Body.GetProperty("title").GetString());
        Assert.Equal("A valid token is required.", result.Body.GetProperty("detail").GetString());
        Assert.DoesNotContain("secret", result.Body.GetRawText());
    }

    [Fact]
    public async Task AnyOtherException_BecomesA500_WithoutLeakingTheMessage()
    {
        var result = await Handle(new InvalidOperationException("Server=secret-host;Password=hunter2"));

        Assert.Equal(500, result.Status);
        Assert.Equal("An unexpected error occurred", result.Body.GetProperty("title").GetString());

        // For a 500 the detail is left out, so internal details (connection strings, stack traces) never reach the client.
        Assert.False(result.Body.TryGetProperty("detail", out _));
        Assert.DoesNotContain("hunter2", result.Body.GetRawText());
    }

    [Fact]
    public async Task EveryResponse_UsesTheProblemJsonContentType()
    {
        var result = await Handle(new NotFoundException("Disbursement", 1));

        Assert.StartsWith("application/problem+json", result.ContentType);
    }

    [Fact]
    public async Task EveryResponse_CarriesTheRequestPathAndATraceId()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/disbursements/5";

        var result = await Handle(new NotFoundException("Disbursement", 5), context);

        Assert.Equal("/api/disbursements/5", result.Body.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(result.Body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ACancelledRequest_IsNotReportedAsAServerError()
    {
        // Simulate the client hanging up: RequestAborted is already cancelled when the exception arrives.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = cts.Token };

        var result = await Handle(new OperationCanceledException(), context);

        Assert.True(result.Handled);
        Assert.Equal(499, result.Status);
        Assert.Equal(JsonValueKind.Undefined, result.Body.ValueKind);   // no body was written
    }

    [Fact]
    public async Task ACancellationNotCausedByTheClient_IsStillAServerError()
    {
        // An OperationCanceledException while the client is still connected (for example a timeout inside
        // the service) is not a client abort, so it must not be hidden behind a 499.
        var result = await Handle(new OperationCanceledException());

        Assert.Equal(500, result.Status);
    }
}
