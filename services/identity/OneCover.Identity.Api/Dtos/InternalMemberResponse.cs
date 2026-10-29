using OneCover.Identity.Api.Models;

namespace OneCover.Identity.Api.Dtos;

/// <summary>Identity-owned member facts returned only to authenticated internal services.</summary>
public sealed record InternalMemberResponse(
    int Id,
    string Role,
    string FirstName,
    string LastName,
    DateOnly HireDate,
    decimal AnnualSalary,
    EmploymentType EmploymentType);
