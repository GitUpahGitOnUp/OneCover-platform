using System.Security.Claims;

namespace OneCover.Identity.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public int UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var userId)
                ? userId
                : throw new UnauthorizedAccessException("The authenticated user ID is missing.");
        }
    }
}
