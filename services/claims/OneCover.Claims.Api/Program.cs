using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OneCover.Claims.Api.Data;
using OneCover.Claims.Api.Errors;

var builder = WebApplication.CreateBuilder(args);

// ---- Required settings -------------------------------------------------------------------
// A helper reads a setting and refuses null, "" and whitespace, stopping startup with a clear message instead
// of failing later (on the first database query, or on the first token). Blank counts as missing because a Docker
// environment variable set to "" or a placeholder in a settings file would otherwise slip through as a
// service that starts but misbehaves.
// (A "local function": a small function declared inside Program.cs that the code around it can call.)
static string RequiredSetting(IConfiguration configuration, string name)
{
    var value = configuration[name];

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Setting '{name}' is missing or blank. Set it with dotnet user-secrets.");
    }

    return value;
}

// ---- Database ----------------------------------------------------------------------------
var connectionString = RequiredSetting(builder.Configuration, "ConnectionStrings:ClaimsDb");

// Register the DbContext in dependency injection (DI) so any class asking for one is given one.
builder.Services.AddDbContext<ClaimsDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---- Our own services --------------------------------------------------------------------
// TimeProvider.System is the real clock. A "singleton" = one shared instance for the whole app.
// Anything that needs "today" or "now" asks this instead of DateTime.Now, so tests can supply a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

// ---- Authentication (JWT) ----------------------------------------------------------------
// Every service checks the JWT itself (CLAUDE.md), even though the gateway checks it too.
// All three settings must match what Identity uses to issue tokens, and a missing or blank one stops startup.
var jwtKey = RequiredSetting(builder.Configuration, "Jwt:Key");
var jwtIssuer = RequiredSetting(builder.Configuration, "Jwt:Issuer");
var jwtAudience = RequiredSetting(builder.Configuration, "Jwt:Audience");

// An HMAC-SHA256 key shorter than 32 bytes is too weak to be safe, so refuse to start with one.
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Setting 'Jwt:Key' must be at least 32 bytes long.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)   // "use JWT bearer tokens to log people in"
    .AddJwtBearer(options =>
    {
        // By default .NET renames incoming claims ("sub" becomes a long nameidentifier URL, "role" becomes
        // a long role URL). false = keep the names exactly as written in the token, so they match the
        // NameClaimType / RoleClaimType settings below and the "sub" lookup in GetUserId.
        options.MapInboundClaims = false;

        // These are the checks a token must pass to be accepted.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,                                  // was it made by the issuer we trust?
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,                                // was it meant for us?
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,                        // was it signed with our secret key?
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,                                // has it expired?
            NameClaimType = "sub",                                  // which claim holds the user's id
            RoleClaimType = "role"                                  // which claim [Authorize(Roles = ...)] checks
        };
    });

builder.Services.AddAuthorization();

// ---- Errors: one ProblemDetails format ---------------------------------------------------
// Every error from every service has the same shape (RFC 9457), built by GlobalExceptionHandler.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ---- Web API -----------------------------------------------------------------------------
builder.Services.AddControllers()
    // Send and accept enums as readable text ("InReview") instead of numbers (1).
    // allowIntegerValues: false also REFUSES numbers on the way in. By default the converter would accept
    // 1 or 99 for a status, and 99 would bind to a value that is not in the enum at all.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// ---- Request pipeline (ORDER MATTERS: each request passes through these top to bottom) ----
app.UseExceptionHandler();   // first, so it can catch errors thrown by everything below

// Exceptions are covered by the handler above, but some errors are not exceptions: a missing or expired token
// (401), a wrong role (403) and an unknown URL (404) leave with an EMPTY body. This fills those in with the same
// ProblemDetails shape, using the AddProblemDetails registration above, so every error looks the same.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();     // 1) work out WHO the caller is (read the token)
app.UseAuthorization();      // 2) check whether that caller is ALLOWED (roles)

app.MapControllers();

app.Run();

// Top-level statements hide the Program class the compiler generates. Declaring it here as public and partial
// lets the integration tests start this whole app in memory with WebApplicationFactory<Program>.
public partial class Program;
