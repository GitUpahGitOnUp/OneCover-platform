using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OneCover.Identity.Api.Middleware;

namespace OneCover.Identity.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task ArgumentException_DoesNotExposeExceptionMessage()
    {
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        var handled = await handler.TryHandleAsync(
            httpContext,
            new ArgumentException("Sensitive internal detail"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);

        responseBody.Position = 0;
        using var body = await JsonDocument.ParseAsync(responseBody);
        Assert.Equal("Invalid request", body.RootElement.GetProperty("detail").GetString());
    }
}
