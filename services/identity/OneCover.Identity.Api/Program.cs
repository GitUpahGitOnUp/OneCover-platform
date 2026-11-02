using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using OneCover.Identity.Api.Data;
using OneCover.Identity.Api.Middleware;
using OneCover.Identity.Api.Repositories;
using OneCover.Identity.Api.Services;

var builder = WebApplication.CreateBuilder(args);

static string RequiredSetting(IConfiguration configuration, string name)
{
    var value = configuration[name];
    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException($"Setting '{name}' is missing or blank.")
        : value;
}

static string RequiredSecretSetting(IConfiguration configuration, string name)
{
    var value = RequiredSetting(configuration, name);
    return value.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase)
        ? throw new InvalidOperationException($"Setting '{name}' must not use a placeholder value.")
        : value;
}

var connectionString = RequiredSetting(builder.Configuration, "ConnectionStrings:IdentityDb");
var jwtKey = RequiredSecretSetting(builder.Configuration, "Jwt:Key");
var jwtIssuer = RequiredSetting(builder.Configuration, "Jwt:Issuer");
var jwtAudience = RequiredSetting(builder.Configuration, "Jwt:Audience");
_ = RequiredSecretSetting(builder.Configuration, "InternalApiKey");
var demoAdminPassword = builder.Configuration["DemoAdmin:Password"];
if (!string.IsNullOrWhiteSpace(demoAdminPassword) &&
    demoAdminPassword.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Setting 'DemoAdmin:Password' must not use a placeholder value.");
}
if (builder.Configuration.GetValue<bool>("DemoAdmin:SeedOnStartup"))
{
    _ = RequiredSetting(builder.Configuration, "DemoAdmin:Email");
    _ = RequiredSecretSetting(builder.Configuration, "DemoAdmin:Password");
}

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long.");
}

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the access token returned by /api/auth/login."
        };
        document.Components.SecuritySchemes["InternalApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = "X-Internal-Api-Key",
            In = ParameterLocation.Header,
            Description = "Shared key for service-to-service endpoints."
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var isInternalEndpoint = context.Description.RelativePath?
            .StartsWith("internal/", StringComparison.OrdinalIgnoreCase) == true;
        var requiresBearer = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>().Any();

        if (isInternalEndpoint || requiresBearer)
        {
            operation.Security ??= [];
            var requirement = new OpenApiSecurityRequirement();
            requirement.Add(
                new OpenApiSecuritySchemeReference(
                    isInternalEndpoint ? "InternalApiKey" : "Bearer",
                    context.Document),
                []);
            operation.Security.Add(requirement);
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddSingleton<IEligibilityService, EligibilityService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
var migrateOnStartup = app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("Database:MigrateOnStartup");
if (migrateOnStartup)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("DemoAdmin:SeedOnStartup"))
{
    await DemoAdminSeeder.SeedAsync(app.Services, app.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options =>
        options.WithTitle("OneCover Identity API")
            .AddHttpAuthentication("Bearer", scheme => scheme.Token = string.Empty)
            .AddApiKeyAuthentication("InternalApiKey", scheme =>
                scheme.Name = "X-Internal-Api-Key"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
