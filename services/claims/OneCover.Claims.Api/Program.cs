using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OneCover.Claims.Api.Data;
using OneCover.Claims.Api.Errors;

var builder = WebApplication.CreateBuilder(args);

// ---- Database ----------------------------------------------------------------------------
// "?? throw ..." = if the setting is missing (null), stop at startup with a clear message
// instead of failing later on the first database query.
var connectionString = builder.Configuration.GetConnectionString("ClaimsDb")
    ?? throw new InvalidOperationException(
        "Connection string 'ClaimsDb' is missing. Set it with: dotnet user-secrets set \"ConnectionStrings:ClaimsDb\" \"...\"");

// Register the DbContext in dependency injection (DI) so any class asking for one is given one.
builder.Services.AddDbContext<ClaimsDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---- Our own services --------------------------------------------------------------------
// TimeProvider.System is the real clock. A "singleton" = one shared instance for the whole app.
// Anything that needs "today" or "now" asks this instead of DateTime.Now, so tests can supply a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

// ---- Authentication (JWT) ----------------------------------------------------------------
// Every service checks the JWT itself (CLAUDE.md), even though the gateway checks it too.
// All three settings must match what Identity uses to issue tokens, and a missing one stops startup.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Setting 'Jwt:Key' is missing. Set it with dotnet user-secrets.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Setting 'Jwt:Issuer' is missing. Set it with dotnet user-secrets.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Setting 'Jwt:Audience' is missing. Set it with dotnet user-secrets.");

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
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// ---- Request pipeline (ORDER MATTERS: each request passes through these top to bottom) ----
app.UseExceptionHandler();   // first, so it can catch errors thrown by everything below

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();     // 1) work out WHO the caller is (read the token)
app.UseAuthorization();      // 2) check whether that caller is ALLOWED (roles)

app.MapControllers();

app.Run();
