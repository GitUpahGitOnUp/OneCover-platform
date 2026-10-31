using System.Net.Http.Headers;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace OneCover.Claims.Tests;

/// <summary>Makes signed JWTs like the ones Identity issues, so tests can call protected endpoints.</summary>
public static class TestTokens
{
    /// <summary>Builds a valid token with the same claim names Identity uses: "sub" for the id and "role" for the role.</summary>
    public static string Create(int userId, string role)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ClaimsApiFactory.JwtIssuer,
            Audience = ClaimsApiFactory.JwtAudience,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = new Dictionary<string, object> { ["sub"] = userId.ToString(), ["role"] = role },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClaimsApiFactory.JwtKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Makes the client send the token the way a browser would: an "Authorization: Bearer ..." header.</summary>
    public static void Authenticate(this HttpClient client, int userId, string role) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Create(userId, role));
}
