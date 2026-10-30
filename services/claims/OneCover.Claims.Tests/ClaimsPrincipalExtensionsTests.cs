using System.Security.Claims;
using OneCover.Claims.Api.Security;

namespace OneCover.Claims.Tests;

/// <summary>Unit tests for reading the signed-in user's id from the JWT claims.</summary>
public class ClaimsPrincipalExtensionsTests
{
    // Builds a fake signed-in user with the given claims. A ClaimsPrincipal is .NET's "the signed-in user";
    // it holds one or more ClaimsIdentity objects, and each identity holds the claims from the token.
    private static ClaimsPrincipal UserWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "TestAuth"));

    [Fact]
    public void GetUserId_ReadsTheSubClaim()
    {
        var user = UserWith(new Claim("sub", "42"));

        Assert.Equal(42, user.GetUserId());
    }

    // Each row is a token that has no usable id: missing, not a number, zero, or negative.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("3fa85f64-5717-4562-b3fc-2c963f66afa6")]   // a Guid: Identity uses int ids, so this must be refused
    public void GetUserId_Throws_WhenTheIdIsMissingOrInvalid(string? sub)
    {
        var user = sub is null ? UserWith() : UserWith(new Claim("sub", sub));

        Assert.Throws<UnauthorizedAccessException>(() => user.GetUserId());
    }

    [Fact]
    public void GetUserId_IgnoresAnIdInAnyOtherClaim()
    {
        // Only "sub" counts. This guards against reading the id from a claim a client could influence.
        var user = UserWith(new Claim(ClaimTypes.NameIdentifier, "42"), new Claim("userId", "42"));

        Assert.Throws<UnauthorizedAccessException>(() => user.GetUserId());
    }
}
