using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OneCover.Claims.Api.Errors;

namespace OneCover.Claims.Tests;

/// <summary>Unit tests for the global exception handler: each exception type becomes the right ProblemDetails response.</summary>
public class GlobalExceptionHandlerTests
{
    // Runs the handler against a fake HTTP request and returns what it would send back.
    // DefaultHttpContext is ASP.NET's ready-made fake request/response. The response body is an in-memory
    // stream, so we can read the JSON the handler wrote. NullLogger is a logger that discards its messages.
    private static async Task<(int Status, string? ContentType, JsonElement Body, bool Handled)> Handle(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Rewind the stream to the start, then read what the handler wrote.
        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);

        return (context.Response.StatusCode, context.Response.ContentType, body.RootElement.Clone(), handled);
    }

    // Each test below throws one kind of exception at the handler and checks the status, title and detail.
    [Fact]
    public async Task NotFoundException_BecomesA404()
    {
        var result = await Handle(new NotFoundException("Claim 5 was not found."));

        Assert.True(result.Handled);
        Assert.Equal(404, result.Status);
        Assert.Equal("Not found", result.Body.GetProperty("title").GetString());
        Assert.Equal("Claim 5 was not found.", result.Body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task BusinessRuleException_BecomesA409()
    {
        var result = await Handle(new BusinessRuleException("Can't move a claim from Closed to InReview."));

        Assert.Equal(409, result.Status);
        Assert.Equal("Business rule violated", result.Body.GetProperty("title").GetString());
        Assert.Equal("Can't move a claim from Closed to InReview.", result.Body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task UnauthorizedAccessException_BecomesA401()
    {
        var result = await Handle(new UnauthorizedAccessException("The token does not contain a valid user id."));

        Assert.Equal(401, result.Status);
        Assert.Equal("Unauthorized", result.Body.GetProperty("title").GetString());
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
        var result = await Handle(new NotFoundException("x"));

        Assert.StartsWith("application/problem+json", result.ContentType);
    }
}
