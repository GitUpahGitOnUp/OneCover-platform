using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using OneCover.Enrollment.Api.Data;
using OneCover.Enrollment.Api.Middleware;
using OneCover.Enrollment.Api.Repositories;
using OneCover.Enrollment.Api.Services;

var builder = WebApplication.CreateBuilder(args);

static string RequiredSetting(IConfiguration configuration, string name)
{
    var value = configuration[name];
    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException($"Setting '{name}' is missing or blank.")
        : value;
}

var connectionString = RequiredSetting(builder.Configuration, "ConnectionStrings:EnrollmentDb");
var jwtKey = RequiredSetting(builder.Configuration, "Jwt:Key");
var jwtIssuer = RequiredSetting(builder.Configuration, "Jwt:Issuer");
var jwtAudience = RequiredSetting(builder.Configuration, "Jwt:Audience");
_ = RequiredSetting(builder.Configuration, "InternalApiKey");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long.");
}

builder.Services.AddDbContext<EnrollmentDbContext>(options =>
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
            Description = "Paste the access token returned by Identity /api/auth/login."
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
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddScoped<ICurrentMember, CurrentMember>();
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
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
if (app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options =>
        options.WithTitle("OneCover Enrollment API")
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
