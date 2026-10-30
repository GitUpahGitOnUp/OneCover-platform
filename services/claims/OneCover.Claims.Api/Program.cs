using Microsoft.EntityFrameworkCore;
using OneCover.Claims.Api.Data;

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
