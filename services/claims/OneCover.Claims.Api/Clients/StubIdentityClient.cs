namespace OneCover.Claims.Api.Clients;

/// <summary>
/// A TEMPORARY stand-in for the Identity service. It gives every member the same salary.
/// Replace it with a real HTTP client for GET /internal/members/{id}, which Identity already has.
/// </summary>
public class StubIdentityClient : IIdentityClient
{
    /// <inheritdoc />
    // 78,000 a year = 1,500 a week, so at 60% the weekly benefit is 900: round numbers that are easy to check by hand.
    public Task<MemberInfo?> GetMemberAsync(int memberId) =>
        Task.FromResult<MemberInfo?>(new MemberInfo(Id: memberId, AnnualSalary: 78_000m));
}
