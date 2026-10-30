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
    private readonly ILogger<GlobalExceptionHandler> _logger;

    // ILogger writes messages to the console/log output. Dependency injection supplies it.
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>Builds the error response. Returning true means "handled, stop looking for another handler".</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
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
            // Show our own messages for the expected cases. For a 500, never leak internal details to the client.
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
        };

        httpContext.Response.StatusCode = status;

        // Write the ProblemDetails as JSON with the standard content type.
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);

        return true;
    }
}
