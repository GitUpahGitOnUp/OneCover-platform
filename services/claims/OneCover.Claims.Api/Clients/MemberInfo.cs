namespace OneCover.Claims.Api.Clients;

/// <summary>
/// What Claims needs to know about a member, as answered by the Identity service: the salary for the benefit math,
/// and the name so admins see "Ada Lovelace" instead of "Member #7".
/// Identity's endpoint returns more (hire date, ...), but Claims only reads the fields it uses.
/// </summary>
// "record" = a class built to carry data. The values in ( ) become read-only properties and the compiler writes
// the constructor. "= null" gives a parameter a DEFAULT, so existing code like new MemberInfo(1, 78000m) still compiles.
/// <param name="Id">The member's id (the Identity user id).</param>
/// <param name="AnnualSalary">The member's annual salary.</param>
/// <param name="FirstName">The member's first name, or null if Identity didn't send one.</param>
/// <param name="LastName">The member's last name, or null if Identity didn't send one.</param>
public record MemberInfo(
    int Id,
    decimal AnnualSalary,
    string? FirstName = null,
    string? LastName = null)
{
    /// <summary>"First Last" for display, or null when Identity gave us no name at all.</summary>
    // A property with a get block is computed and read-only: worked out each time it is read, never filled from JSON.
    public string? DisplayName
    {
        get
        {
            // Join the two parts with a space, then .Trim() = cut spaces off both ends, so a missing first or last
            // name doesn't leave a stray space.
            var name = $"{FirstName} {LastName}".Trim();

            // An empty result means "no name known", which we report as null rather than "".
            return name.Length == 0 ? null : name;
        }
    }
}
