using System.Net;
using Microsoft.Extensions.Options;
using OneCover.Contracts.Events;

namespace OneCover.Claims.Api.Clients;

/// <summary>
/// Sends events to Disbursements as an HTTP POST to /internal/events/claim-approved. The base address and the
/// X-Internal-Api-Key header are set up once in Program.cs when the HttpClient is created.
/// </summary>
public class HttpClaimEventPublisher : IClaimEventPublisher
{
    // The path on Disbursements that receives the event (no leading slash, so it is added to the base address).
    private const string ClaimApprovedPath = "internal/events/claim-approved";

    private readonly HttpClient _http;
    private readonly DisbursementsOptions _options;
    private readonly ILogger<HttpClaimEventPublisher> _logger;

    // Constructor injection. IOptions<T> hands over the settings object bound from configuration.
    public HttpClaimEventPublisher(HttpClient http, IOptions<DisbursementsOptions> options, ILogger<HttpClaimEventPublisher> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> PublishClaimApprovedAsync(ClaimApproved claimApproved)
    {
        // At least one attempt, even if the setting is mistakenly 0.
        var maxAttempts = Math.Max(1, _options.MaxAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // PostAsJsonAsync turns the record into JSON (a DateOnly becomes "yyyy-MM-dd") and POSTs it.
                // There is deliberately NO cancellation token from the web request: the claim is already saved as
                // Approved, so the event must still go out even if the admin closes their browser tab now.
                // "using" disposes the response when we are done with it.
                using var response = await _http.PostAsJsonAsync(ClaimApprovedPath, claimApproved);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                // A 4xx such as 401 (wrong key) or 409 (Disbursements refused the data) will not fix itself, so
                // retrying is pointless. Stop and report it.
                if (!IsWorthRetrying(response.StatusCode))
                {
                    _logger.LogError(
                        "Disbursements refused ClaimApproved for claim {ClaimId} with status {StatusCode}. Not retrying.",
                        claimApproved.ClaimId, (int)response.StatusCode);
                    return false;
                }

                _logger.LogWarning(
                    "Disbursements answered {StatusCode} to ClaimApproved for claim {ClaimId} (attempt {Attempt} of {Max}).",
                    (int)response.StatusCode, claimApproved.ClaimId, attempt, maxAttempts);
            }
            // The network call itself failed (service down, connection refused, DNS) ...
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Could not reach Disbursements for claim {ClaimId} (attempt {Attempt} of {Max}).",
                    claimApproved.ClaimId, attempt, maxAttempts);
            }
            // ... or HttpClient's own timeout fired (it reports a timeout as a TaskCanceledException).
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Disbursements timed out for claim {ClaimId} (attempt {Attempt} of {Max}).",
                    claimApproved.ClaimId, attempt, maxAttempts);
            }

            // Wait a little longer after each failure (300 ms, 600 ms, ...) so a service that is restarting gets time.
            if (attempt < maxAttempts)
            {
                await Task.Delay(_options.RetryDelay * attempt);
            }
        }

        _logger.LogError(
            "ClaimApproved for claim {ClaimId} was NOT delivered after {Max} attempts. The claim is Approved but has no disbursement yet.",
            claimApproved.ClaimId, maxAttempts);
        return false;
    }

    // Server trouble (5xx) and "slow down / try later" (408, 429) are usually temporary, so they are worth another try.
    private static bool IsWorthRetrying(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
}
