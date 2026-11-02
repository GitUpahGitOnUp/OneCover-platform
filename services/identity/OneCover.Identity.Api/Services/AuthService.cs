using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OneCover.Identity.Api.Dtos;
using OneCover.Identity.Api.Models;
using OneCover.Identity.Api.Repositories;

namespace OneCover.Identity.Api.Services;

public sealed class AuthService(
    IUserRepository users,
    IConfiguration configuration,
    TimeProvider timeProvider) : IAuthService
{
    private readonly PasswordHasher<User> passwordHasher = new();

    public async Task<AuthResponse> RegisterAsync(
        RegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var hireDate = request.HireDate!.Value;
        if (hireDate > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new ArgumentException("Hire date cannot be in the future.", nameof(request));
        }

        var user = new User
        {
            Email = email,
            Role = Role.Member,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            HireDate = hireDate,
            AnnualSalary = request.AnnualSalary!.Value,
            EmploymentType = request.EmploymentType!.Value,
            SsnLast4 = request.SsnLast4,
            CreatedAt = timeProvider.GetUtcNow()
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        await users.AddAsync(user, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        if (user is null ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) ==
            PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return CreateAuthResponse(user);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var now = timeProvider.GetUtcNow();
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim("sub", user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("role", user.Role.ToString())
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddHours(2).UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), user.Role.ToString());
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
