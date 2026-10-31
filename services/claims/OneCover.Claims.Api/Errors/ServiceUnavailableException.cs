namespace OneCover.Claims.Api.Errors;

/// <summary>
/// Thrown when another service this request depends on can't be reached (for example Disbursements is down
/// while an admin clicks "Re-send payout"). The global exception handler turns it into an HTTP 503 ProblemDetails.
/// </summary>
// 503 (not 409) is the honest answer: the request itself was fine, so retrying later is the right move.
public class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message) : base(message)
    {
    }
}
