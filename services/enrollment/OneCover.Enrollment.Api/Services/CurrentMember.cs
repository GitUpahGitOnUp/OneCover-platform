using System.Security.Claims;

namespace OneCover.Enrollment.Api.Services;

public sealed class CurrentMember(IHttpContextAccessor httpContextAccessor) : ICurrentMember
{
    public int MemberId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var memberId)
                ? memberId
                : throw new UnauthorizedAccessException("The authenticated member ID is missing.");
        }
    }
}
