namespace OneCover.Disbursements.Tests;

/// <summary>Integration tests that start the real Disbursements API in memory: the startup checks.</summary>
public class ApiStartupTests
{
    // [Theory] runs the same test once per row. Each row is a setting that is present but unusable.
    [Theory]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Issuer", "   ")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:Audience", "   ")]
    [InlineData("Jwt:Key", "")]
    [InlineData("Jwt:Key", "                                   ")]   // 35 spaces: long enough, but not a real key
    [InlineData("Jwt:Key", "too-short")]
    [InlineData("ConnectionStrings:DisbursementsDb", "")]
    [InlineData("ConnectionStrings:DisbursementsDb", "   ")]
    public void StartupIsRefused_WhenARequiredSettingIsBlankOrTooWeak(string name, string value)
    {
        using var factory = new DisbursementsApiFactory().With(name, value);

        // The app throws while building, and the test host may wrap that error, so look through the whole chain.
        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains(name, AllMessages(error));
    }

    [Fact]
    public void TheAppStarts_WhenEverySettingIsValid()
    {
        using var factory = new DisbursementsApiFactory();

        var error = Record.Exception(() => factory.CreateClient());

        Assert.Null(error);
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
