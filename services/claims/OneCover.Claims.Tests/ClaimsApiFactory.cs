using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OneCover.Claims.Tests;

/// <summary>
/// Starts the whole Claims API in memory for integration tests, with fake settings so the tests run on any
/// machine (a fresh clone or CI) without user-secrets or a real database.
/// </summary>
// WebApplicationFactory<Program> boots the real Program.cs (the real pipeline, handler and auth setup)
// inside the test process and gives back an HttpClient that talks to it directly, with no network.
public class ClaimsApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings = new()
    {
        // Never connected to in these tests: the app only needs the setting to exist so it will start.
        ["ConnectionStrings:ClaimsDb"] = "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True",
        ["Jwt:Key"] = "test-only-key-that-is-at-least-32-bytes-long!",
        ["Jwt:Issuer"] = "test-issuer",
        ["Jwt:Audience"] = "test-audience"
    };

    /// <summary>Overrides one setting, for example blank out the issuer to check that startup is refused.</summary>
    public ClaimsApiFactory With(string name, string? value)
    {
        _settings[name] = value;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" is not "Development", so the developer's own user-secrets are NOT loaded and these tests
        // behave the same on every machine.
        builder.UseEnvironment("Testing");

        foreach (var (name, value) in _settings)
        {
            builder.UseSetting(name, value);
        }
    }
}
