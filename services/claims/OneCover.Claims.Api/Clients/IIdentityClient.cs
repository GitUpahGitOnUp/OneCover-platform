namespace OneCover.Claims.Api.Clients;

/// <summary>Asks the Identity service about a member. Claims has no users table, so the salary has to come from here.</summary>
public interface IIdentityClient
{
    /// <summary>Gets a member's salary, or null if Identity has no such member.</summary>
    /// <param name="memberId">The member to look up.</param>
    Task<MemberInfo?> GetMemberAsync(int memberId);
}
