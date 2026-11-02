using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OneCover.Enrollment.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var configuredKey = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["InternalApiKey"];
        var providedKey = context.HttpContext.Request.Headers["X-Internal-Api-Key"].ToString();

        if (string.IsNullOrEmpty(configuredKey) || string.IsNullOrEmpty(providedKey))
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        if (configuredBytes.Length != providedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes))
        {
            context.Result = new UnauthorizedResult();
        }

        return Task.CompletedTask;
    }
}
