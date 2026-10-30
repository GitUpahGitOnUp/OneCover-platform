namespace OneCover.Claims.Api.Errors;

/// <summary>
/// Thrown when a requested record doesn't exist OR belongs to someone else.
/// Both cases look identical on purpose, so the API never confirms that another member's record exists.
/// The global exception handler turns this into an HTTP 404 ProblemDetails response.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    /// Creates the exception with a FIXED message built from the resource name and id, e.g. "Claim 5 was not found."
    /// </summary>
    /// <param name="resource">What was looked up, for example "Claim".</param>
    /// <param name="id">The id that was asked for.</param>
    // There is deliberately no constructor that takes free text. If callers could write their own message,
    // one might write "Claim 5 belongs to another member" and the different wording would confirm the record
    // exists. A fixed message makes "doesn't exist" and "isn't yours" produce exactly the same response.
    // ": base(...)" passes the text up to Exception so it is stored in the .Message property.
    public NotFoundException(string resource, int id) : base($"{resource} {id} was not found.")
    {
    }
}
