namespace OneCover.Disbursements.Api.Errors;

/// <summary>
/// Thrown when a request is valid in shape but breaks a business rule
/// (for example, "only pending disbursements can be marked paid").
/// A global exception handler will turn this into an HTTP 409 Conflict ProblemDetails response.
/// </summary>
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
