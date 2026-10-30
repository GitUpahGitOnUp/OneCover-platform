using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OneCover.Claims.Api.Errors;

/// <summary>
/// Catches every unhandled exception and answers with a ProblemDetails (RFC 9457) JSON body,
/// so every error from this service has the same shape as the other services.
/// </summary>
// IExceptionHandler is ASP.NET Core's hook for "an exception escaped, what response should we send?"
public class GlobalExceptionHandler : IExceptionHandler
{
    // 499 is the widely used, unofficial "client closed the request" status. Nobody receives it (the client is gone),
    // but it keeps the logs and metrics honest: an abort is not a server error.
    private const int ClientClosedRequest = 499;

    private readonly ILogger<GlobalExceptionHandler> _logger;

    // ILogger writes messages to the console/log output. Dependency injection supplies it.
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>Builds the error response. Returning true means "handled, stop looking for another handler".</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // If the client hung up (closed the browser tab, cancelled the call), the work was cancelled on purpose.
        // That is not a bug, so don't log an error and don't try to write a body nobody will read.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        // A "switch expression": picks a (status code, title) pair based on the exception's type.
        // "_" is the catch-all for anything we didn't list.
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            BusinessRuleException => (StatusCodes.Status409Conflict, "Business rule violated"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        // Log unexpected failures with the full exception. Expected ones (404/409/401) are not worth an error log.
        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,

            // What the client may read:
            //   500 -> nothing, so internal details never leak.
            //   401 -> a fixed sentence. UnauthorizedAccessException is also thrown by .NET itself (for example
            //          on a file permission problem), and its message must not reach the client.
            //   404/409 -> our own messages, which are written to be safe to show.
            Detail = status switch
            {
                StatusCodes.Status500InternalServerError => null,
                StatusCodes.Status401Unauthorized => "A valid token is required.",
                _ => exception.Message
            },

            // Instance = the URL that was requested, as RFC 9457 describes.
            Instance = httpContext.Request.Path
        };

        // traceId lets someone who reports an error point us at the exact request in the logs.
        // Activity.Current is the distributed trace id when there is one; otherwise ASP.NET's own request id.
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;

        // Write the ProblemDetails as JSON with the standard content type.
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);

        return true;
    }
}
