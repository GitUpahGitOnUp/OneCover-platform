namespace OneCover.Disbursements.Api.Errors;

/// <summary>
/// Thrown when a requested record doesn't exist OR belongs to someone else.
/// Both cases look identical on purpose, so the API never confirms that another member's record exists.
/// A global exception handler will turn this into an HTTP 404 ProblemDetails response.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
