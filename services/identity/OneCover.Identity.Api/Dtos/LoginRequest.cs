using System.ComponentModel.DataAnnotations;

namespace OneCover.Identity.Api.Dtos;

/// <summary>Credentials submitted to obtain a signed access token.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}
