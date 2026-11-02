namespace OneCover.Claims.Api.Clients;

/// <summary>
/// A canned Identity client for isolated tests and local development.
/// </summary>
public class StubIdentityClient : IIdentityClient
{
    /// <inheritdoc />
    // 78,000 a year = 1,500 a week, so at 60% the weekly benefit is 900: round numbers that are easy to check by hand.
    public Task<MemberInfo?> GetMemberAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<MemberInfo?>(new MemberInfo(
            Id: memberId,
            AnnualSalary: 78_000m,
            FirstName: "Test",
            LastName: $"Member {memberId}"));
    }
}
