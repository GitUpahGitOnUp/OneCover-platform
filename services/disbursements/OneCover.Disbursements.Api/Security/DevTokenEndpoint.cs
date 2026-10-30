using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace OneCover.Disbursements.Api.Security;

/// <summary>
/// DEVELOPMENT ONLY. Issues a test JWT so the Member and Admin endpoints can be called before the
/// Identity service exists. Program.cs only maps it when running in the Development environment.
/// </summary>
public static class DevTokenEndpoint
{
    /// <summary>Adds GET /dev/token?userId=1&amp;role=Admin to the app.</summary>
    // "this WebApplication app" = extension method, so Program.cs can write app.MapDevToken().
    public static void MapDevToken(this WebApplication app)
    {
        // MapGet = answer GET requests at this URL. userId and role come from the query string.
        // IConfiguration and TimeProvider are supplied by DI because they're listed as parameters.
        app.MapGet("/dev/token", (int userId, string role, IConfiguration config, TimeProvider time) =>
        {
            // Build the signing key from the same secret that token validation uses.
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));

            // The "descriptor" describes the token: who made it, who it's for, what claims it carries, when it expires.
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = config["Jwt:Issuer"],
                Audience = config["Jwt:Audience"],
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("sub", userId.ToString()),   // the member id (read by GetMemberId)
                    new Claim("role", role)                // "Member" or "Admin"
                }),
                Expires = time.GetUtcNow().UtcDateTime.AddHours(8),
                // Sign the token so the service can verify nobody tampered with it.
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            // CreateToken turns the descriptor into the final "xxxxx.yyyyy.zzzzz" token string.
            return Results.Ok(new { token = new JsonWebTokenHandler().CreateToken(descriptor) });
        })
        .AllowAnonymous();   // this endpoint itself needs no login
    }
}
