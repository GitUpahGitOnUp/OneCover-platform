using System.ComponentModel.DataAnnotations;
using OneCover.Identity.Api.Models;

namespace OneCover.Identity.Api.Dtos;

/// <summary>Creates a member account. Public registration always assigns the Member role.</summary>
public sealed record RegistrationRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    public DateOnly? HireDate { get; init; }

    [Required, Range(typeof(decimal), "0", "10000000")]
    public decimal? AnnualSalary { get; init; }

    [Required, EnumDataType(typeof(EmploymentType))]
    public EmploymentType? EmploymentType { get; init; }

    [Required, RegularExpression(@"^\d{4}$")]
    public string SsnLast4 { get; init; } = string.Empty;
}
