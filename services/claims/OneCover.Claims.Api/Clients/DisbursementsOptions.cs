namespace OneCover.Claims.Api.Clients;

/// <summary>Settings for calling the Disbursements service. Bound from the "Disbursements" section of the configuration.</summary>
public class DisbursementsOptions
{
    /// <summary>The name of the configuration section these values come from.</summary>
    public const string SectionName = "Disbursements";

    /// <summary>Where Disbursements listens, such as http://localhost:5175 (env var Disbursements__BaseUrl in Docker).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>How many times to try sending an event before giving up.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>The wait before the second try. Each later wait is longer (this value times the attempt number).</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(300);
}
