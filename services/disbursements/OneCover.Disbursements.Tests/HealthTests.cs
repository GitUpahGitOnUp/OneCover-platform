using System.Net;

namespace OneCover.Disbursements.Tests;

/// <summary>Tests for the /health endpoint that Docker and the gateway use to ask "is this service alive?".</summary>
public class HealthTests
{
    [Fact]
    public async Task Health_AnswersOk_WithoutAToken()
    {
        // No WithService: /health must not need the service layer or a database (the connection string is fake here).
        using var factory = new DisbursementsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
