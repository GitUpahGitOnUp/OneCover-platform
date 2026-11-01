namespace OneCover.Claims.Api.Clients;

/// <summary>
/// What Claims needs to know about a member, as answered by the Identity service: just the salary for the benefit math.
/// Identity's endpoint returns more (name, hire date, ...), but Claims only reads the fields it uses.
/// </summary>
/// <param name="Id">The member's id (the Identity user id).</param>
/// <param name="AnnualSalary">The member's annual salary.</param>
public record MemberInfo(int Id, decimal AnnualSalary);
