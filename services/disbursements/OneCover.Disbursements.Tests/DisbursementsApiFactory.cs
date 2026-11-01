using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneCover.Disbursements.Api.Services;

namespace OneCover.Disbursements.Tests;

/// <summary>
/// Starts the whole Disbursements API in memory for integration tests, with fake settings so the tests run on
/// any machine (a fresh clone or CI) without user-secrets or a real database.
/// </summary>
// WebApplicationFactory<Program> boots the real Program.cs (the real pipeline, handler and auth setup)
// inside the test process and gives back an HttpClient that talks to it directly, with no network.
public class DisbursementsApiFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "test-only-key-that-is-at-least-32-bytes-long!";
    public const string JwtIssuer = "test-issuer";
    public const string JwtAudience = "test-audience";
    public const string InternalKey = "test-internal-key";

    private readonly Dictionary<string, string?> _settings = new()
    {
        // Never connected to in these tests: the app only needs the setting to exist so it will start.
        ["ConnectionStrings:DisbursementsDb"] = "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True",
        ["Jwt:Key"] = JwtKey,
        ["Jwt:Issuer"] = JwtIssuer,
        ["Jwt:Audience"] = JwtAudience,
        ["InternalApiKey"] = InternalKey
    };

    private IDisbursementService? _service;

    /// <summary>Overrides one setting, for example blank out the issuer to check that startup is refused.</summary>
    public DisbursementsApiFactory With(string name, string? value)
    {
        _settings[name] = value;
        return this;
    }

    /// <summary>
    /// Swaps the real service (which needs a database) for a fake one the test controls.
    /// The controllers, authorization, error handler and JSON settings are all still the real ones.
    /// </summary>
    public DisbursementsApiFactory WithService(IDisbursementService service)
    {
        _service = service;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" is not "Development", so the developer's own user-secrets and the dev token endpoint
        // are NOT loaded and these tests behave the same on every machine.
        builder.UseEnvironment("Testing");

        foreach (var (name, value) in _settings)
        {
            builder.UseSetting(name, value);
        }

        if (_service is not null)
        {
            // RemoveAll deletes the real registration, then the fake takes its place for this run.
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDisbursementService>();
                services.AddSingleton(_service);
            });
        }
    }
}
