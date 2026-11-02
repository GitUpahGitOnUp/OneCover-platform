using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneCover.Identity.Api.Models;

namespace OneCover.Identity.Api.Data;

public static class DemoAdminSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var email = configuration["DemoAdmin:Email"];
        var password = configuration["DemoAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            return;
        }

        var admin = new User
        {
            Email = normalizedEmail,
            Role = Role.Admin,
            FirstName = "Development",
            LastName = "Administrator",
            HireDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1),
            AnnualSalary = 0m,
            EmploymentType = EmploymentType.FullTime,
            SsnLast4 = "0000",
            CreatedAt = DateTimeOffset.UtcNow
        };
        admin.PasswordHash = new PasswordHasher<User>()
            .HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
    }
}
