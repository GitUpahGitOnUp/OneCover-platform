using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Tests;

/// <summary>Integration tests that start the real Claims API in memory: configuration checks and the error format.</summary>
public class ClaimsApiStartupTests
{
    [Fact]
    public async Task AnUnknownUrl_ReturnsA404_InTheProblemDetailsFormat()
    {
        // A 404 from routing is not an exception, so the exception handler never sees it.
        // UseStatusCodePages is what fills in the body with the shared ProblemDetails shape.
        using var factory = new ClaimsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
    }

    // [Theory] runs the same test once per row. Each row is a setting that is present but unusable.
    [Theory]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Issuer", "   ")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:Audience", "   ")]
    [InlineData("Jwt:Key", "")]
    [InlineData("Jwt:Key", "                                   ")]   // 35 spaces: long enough, but not a real key
    [InlineData("Jwt:Key", "too-short")]
    public void StartupIsRefused_WhenAJwtSettingIsBlankOrTooWeak(string name, string value)
    {
        using var factory = new ClaimsApiFactory().With(name, value);

        // The app throws while building, and the test host may wrap that error, so look through the whole chain.
        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains(name, AllMessages(error));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StartupIsRefused_WhenTheConnectionStringIsBlank(string value)
    {
        using var factory = new ClaimsApiFactory().With("ConnectionStrings:ClaimsDb", value);

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("ConnectionStrings:ClaimsDb", AllMessages(error));
    }

    [Fact]
    public void EnumsInJson_AcceptNames_ButRefuseNumbers()
    {
        using var factory = new ClaimsApiFactory();
        _ = factory.CreateClient();   // starts the app so its services exist

        // Ask the app for the exact JSON settings its controllers use.
        var json = factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        Assert.Equal(ClaimStatus.InReview, JsonSerializer.Deserialize<ClaimStatus>("\"InReview\"", json));

        // 1 is a real enum value and 99 is not. Both are refused, so a number can't sneak past the names.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClaimStatus>("1", json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClaimStatus>("99", json));
    }

    // Joins the message of an exception and every exception inside it, so a wrapped startup error can be searched.
    private static string AllMessages(Exception? error)
    {
        var messages = new List<string>();
        for (var current = error; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}
