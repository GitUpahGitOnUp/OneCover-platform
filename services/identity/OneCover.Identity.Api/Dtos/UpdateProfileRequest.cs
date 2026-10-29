using System.ComponentModel.DataAnnotations;

namespace OneCover.Identity.Api.Dtos;

/// <summary>Fields a member may edit on their own profile.</summary>
public sealed record UpdateProfileRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, RegularExpression(@"^\d{4}$")]
    public string SsnLast4 { get; init; } = string.Empty;
}
