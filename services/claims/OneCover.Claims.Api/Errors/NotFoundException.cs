namespace OneCover.Claims.Api.Errors;

/// <summary>
/// Thrown when a requested record doesn't exist OR belongs to someone else.
/// Both cases look identical on purpose, so the API never confirms that another member's record exists.
/// The global exception handler turns this into an HTTP 404 ProblemDetails response.
/// </summary>
public class NotFoundException : Exception
{
    // ": base(message)" passes the text up to Exception so it is stored in the .Message property.
    public NotFoundException(string message) : base(message)
    {
    }
}
