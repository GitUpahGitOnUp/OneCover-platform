using System.Net.Http.Headers;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace OneCover.Disbursements.Tests;

/// <summary>Makes signed JWTs like the ones Identity issues, so tests can call protected endpoints.</summary>
public static class TestTokens
{
    /// <summary>Builds a valid token with the same claim names Identity uses: "sub" for the id and "role" for the role.</summary>
    public static string Create(int userId, string role, string? key = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = DisbursementsApiFactory.JwtIssuer,
            Audience = DisbursementsApiFactory.JwtAudience,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = new Dictionary<string, object> { ["sub"] = userId.ToString(), ["role"] = role },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? DisbursementsApiFactory.JwtKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Makes the client send the token the way a browser would: an "Authorization: Bearer ..." header.</summary>
    public static void Authenticate(this HttpClient client, int userId, string role) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Create(userId, role));
}
