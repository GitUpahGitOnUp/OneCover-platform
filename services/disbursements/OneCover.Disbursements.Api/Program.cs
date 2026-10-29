using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OneCover.Disbursements.Api.Data;
using OneCover.Disbursements.Api.Errors;
using OneCover.Disbursements.Api.Repositories;
using OneCover.Disbursements.Api.Security;
using OneCover.Disbursements.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Database ----------------------------------------------------------------------------
// "?? throw ..." = if the setting is missing (null), stop at startup with a clear message
// instead of failing later on the first database query.
var connectionString = builder.Configuration.GetConnectionString("DisbursementsDb")
    ?? throw new InvalidOperationException(
        "Connection string 'DisbursementsDb' is missing. Set it with: dotnet user-secrets set \"ConnectionStrings:DisbursementsDb\" \"...\"");

// Register the DbContext in dependency injection (DI) so any class asking for one is given one.
builder.Services.AddDbContext<DisbursementsDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---- Our own services --------------------------------------------------------------------
// "Scoped" = one instance per HTTP request, which matches the DbContext's lifetime.
builder.Services.AddScoped<IDisbursementRepository, DisbursementRepository>();
builder.Services.AddScoped<IDisbursementService, DisbursementService>();

// TimeProvider.System is the real clock. A "singleton" = one shared instance for the whole app.
// Tests can replace it with a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

// ---- Authentication (JWT) ----------------------------------------------------------------
// Every service checks the JWT itself (CLAUDE.md), even though the gateway checks it too.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Setting 'Jwt:Key' is missing. Set it with dotnet user-secrets.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)   // "use JWT bearer tokens to log people in"
    .AddJwtBearer(options =>
    {
        // These are the checks a token must pass to be accepted.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,                                  // was it made by the issuer we trust?
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,                                // was it meant for us?
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,                        // was it signed with our secret key?
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,                                // has it expired?
            NameClaimType = "sub",                                  // which claim holds the user's id
            RoleClaimType = "role"                                  // which claim [Authorize(Roles = ...)] checks
        };
    });

builder.Services.AddAuthorization();

// ---- Errors: one ProblemDetails format ---------------------------------------------------
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ---- Web API -----------------------------------------------------------------------------
builder.Services.AddControllers()
    // Send and accept enums as readable text ("Paid") instead of numbers (1).
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

var app = builder.Build();

// ---- Request pipeline (ORDER MATTERS: each request passes through these top to bottom) ----
app.UseExceptionHandler();   // first, so it can catch errors thrown by everything below

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapDevToken();       // the test-token endpoint exists ONLY in Development
}

app.UseHttpsRedirection();

app.UseAuthentication();     // 1) work out WHO the caller is (read the token)
app.UseAuthorization();      // 2) check whether that caller is ALLOWED (roles)

app.MapControllers();

app.Run();
