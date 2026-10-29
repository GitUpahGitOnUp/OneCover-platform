using OneCover.Identity.Api.Models;

namespace OneCover.Identity.Api.Dtos;

/// <summary>The authenticated user's profile and computed FMLA eligibility.</summary>
public sealed record ProfileResponse(
    int Id,
    string Email,
    string Role,
    string FirstName,
    string LastName,
    DateOnly HireDate,
    decimal AnnualSalary,
    EmploymentType EmploymentType,
    string SsnLast4,
    bool IsFmlaEligible);
