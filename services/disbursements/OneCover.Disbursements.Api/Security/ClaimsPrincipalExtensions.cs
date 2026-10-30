using System.Security.Claims;

namespace OneCover.Disbursements.Api.Security;

/// <summary>Helpers for reading facts about the signed-in user from their JWT.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the signed-in member's id from the token. Throws UnauthorizedAccessException if the token has no valid id.
    /// </summary>
    // ClaimsPrincipal = .NET's object for "the signed-in user and all their claims". In a controller it is `User`.
    // "this ClaimsPrincipal user" makes this an extension method, so controllers can write User.GetMemberId().
    public static int GetMemberId(this ClaimsPrincipal user)
    {
        // A JWT stores the user's id in the standard "sub" (subject) claim. Depending on settings,
        // .NET may rename it to ClaimTypes.NameIdentifier, so check both.
        // FindFirstValue returns the claim's text value, or null if the claim isn't there.
        var value = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        // int.TryParse tries to turn the text into a number WITHOUT throwing if it fails.
        // It returns true/false, and "out var id" receives the number when it works.
        if (!int.TryParse(value, out var id))
        {
            throw new UnauthorizedAccessException("The token does not contain a valid member id.");
        }

        return id;
    }
}
