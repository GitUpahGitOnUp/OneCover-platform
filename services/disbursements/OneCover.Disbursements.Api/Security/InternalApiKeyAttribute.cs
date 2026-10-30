using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OneCover.Disbursements.Api.Security;

/// <summary>
/// Guards service-to-service endpoints. The caller must send the shared secret in the
/// "X-Internal-Api-Key" header, and it must equal the "InternalApiKey" setting.
/// Put [InternalApiKey] on a controller or an action to protect it.
/// </summary>
// An "attribute" is a label you put in [square brackets] above a class or method.
// "sealed" = nobody can inherit from this class; it is final.
// AttributeUsage says where the label is allowed: on a class or on a method.
// IAsyncAuthorizationFilter is a hook ASP.NET runs BEFORE the action; if we set context.Result,
// the request stops there and the action never runs.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    // The name of the HTTP header the caller puts the key in. A const never changes at runtime.
    private const string HeaderName = "X-Internal-Api-Key";

    /// <summary>Runs before the action. Lets the request through only if the key is present and correct.</summary>
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Read the key we expect from configuration (user-secrets locally, env var in Docker).
        // RequestServices is the dependency-injection container, so we can ask it for IConfiguration.
        var configuredKey = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["InternalApiKey"];

        // Read the key the caller sent. .ToString() turns "no header" into "" so we can test it simply.
        var providedKey = context.HttpContext.Request.Headers[HeaderName].ToString();

        // If either key is missing, reject. A missing configured key also rejects, so a
        // forgotten setting can never leave the endpoint open.
        if (string.IsNullOrEmpty(configuredKey) || string.IsNullOrEmpty(providedKey))
        {
            Reject(context);
            return Task.CompletedTask;
        }

        // Compare the two keys as bytes. FixedTimeEquals always takes the same time whether the keys
        // match or not, so an attacker cannot guess the key by measuring how fast we say "no".
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        if (configuredBytes.Length != providedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes))
        {
            Reject(context);
        }

        // Task.CompletedTask = "nothing left to wait for". Nothing in this method needs await.
        return Task.CompletedTask;
    }

    // Stops the request with a 401 in the same ProblemDetails format as every other error.
    private static void Reject(AuthorizationFilterContext context)
    {
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "A valid internal API key is required."
        })
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };
    }
}
