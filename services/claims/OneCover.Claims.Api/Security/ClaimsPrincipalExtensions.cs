using System.Security.Claims;

namespace OneCover.Claims.Api.Security;

/// <summary>Helpers for reading facts about the signed-in user from their JWT.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the signed-in user's id from the token: the member when filing a claim, the admin when changing its status.
    /// Throws UnauthorizedAccessException (a 401) if the token has no valid id.
    /// The id always comes from the token, never from the URL or request body (CLAUDE.md rule).
    /// </summary>
    // ClaimsPrincipal = .NET's object for "the signed-in user and all their claims". In a controller it is `User`.
    // "this ClaimsPrincipal user" makes this an extension method, so controllers can write User.GetUserId().
    public static int GetUserId(this ClaimsPrincipal user)
    {
        // A JWT stores the user's id in the standard "sub" (subject) claim. Identity issues it as "sub", and
        // JWT inbound claim renaming is switched off in Program.cs, so it keeps that exact name.
        // FindFirstValue returns the claim's text value, or null if the claim isn't there.
        var value = user.FindFirstValue("sub");

        // int.TryParse tries to turn the text into a number WITHOUT throwing if it fails.
        // It returns true/false, and "out var id" receives the number when it works.
        // A valid id is positive (database ids start at 1), so 0 and negatives are refused too.
        if (!int.TryParse(value, out var id) || id <= 0)
        {
            throw new UnauthorizedAccessException("The token does not contain a valid user id.");
        }

        return id;
    }
}
