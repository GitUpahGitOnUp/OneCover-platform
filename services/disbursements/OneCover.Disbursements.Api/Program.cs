using Microsoft.EntityFrameworkCore;
using OneCover.Disbursements.Api.Data;
using OneCover.Disbursements.Api.Repositories;
using OneCover.Disbursements.Api.Services;


var builder = WebApplication.CreateBuilder(args);

// Register the DbContext in the dependency injection (DI) container, so any class that
// asks for a DisbursementsDbContext in its constructor is given one automatically.
// UseSqlServer = use the SQL Server provider.
// GetConnectionString("DisbursementsDb") reads ConnectionStrings:DisbursementsDb from
// configuration (user-secrets locally, an environment variable in Docker).
// "?? throw ..." = if the setting is missing (null), stop the app at startup with a clear message
// instead of failing later on the first database query.
var connectionString = builder.Configuration.GetConnectionString("DisbursementsDb")
    ?? throw new InvalidOperationException(
        "Connection string 'DisbursementsDb' is missing. Set it with: dotnet user-secrets set \"ConnectionStrings:DisbursementsDb\" \"...\"");

builder.Services.AddDbContext<DisbursementsDbContext>(options =>
    options.UseSqlServer(connectionString));

// "Scoped" = one repository instance per HTTP request, which matches the DbContext's lifetime.
// When a class asks for IDisbursementRepository, DI hands it a DisbursementRepository.
builder.Services.AddScoped<IDisbursementRepository, DisbursementRepository>();

// TimeProvider.System is the real clock. Registered once as a "singleton" (one shared instance
// for the whole app). Tests can replace it with a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

// Scoped, like the repository: one service instance per HTTP request.
builder.Services.AddScoped<IDisbursementService, DisbursementService>();


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
