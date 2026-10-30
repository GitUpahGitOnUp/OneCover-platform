namespace OneCover.Claims.Api.Errors;

/// <summary>
/// Thrown when a request is valid in shape but breaks a business rule
/// (for example, "a claim can't move from Closed to InReview").
/// A global exception handler will turn this into an HTTP 409 Conflict ProblemDetails response.
/// </summary>
// Each service has its own copy of this class on purpose. Sharing it through contracts/ would make the
// services depend on each other's code, and contracts/ is only for DTOs and events.
// ": Exception" means this class INHERITS from .NET's built-in Exception class,
// so it can be thrown with "throw new BusinessRuleException(...)" and caught like any exception.
public class BusinessRuleException : Exception
{
    // Constructor: runs when you write "new BusinessRuleException("text")".
    // ": base(message)" passes the text up to Exception so it is stored in the .Message property.
    public BusinessRuleException(string message) : base(message)
    {
    }
}
