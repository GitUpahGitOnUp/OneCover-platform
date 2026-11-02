using System.ComponentModel.DataAnnotations;
using OneCover.Claims.Api.Models;

namespace OneCover.Claims.Api.Dtos;

/// <summary>
/// The body of POST /api/claims. It deliberately has NO amount fields and no member id: the benefit is calculated
/// on the server and the member is taken from the JWT (CLAUDE.md rules).
/// </summary>
// The validation attributes ([Range], [Required], ...) sit on the constructor parameters, NOT on the generated
// properties ("[property: ...]"). For a positional record MVC reads the rules from the parameters, and it throws
// an error at request time if it finds rules on the properties instead.
/// <param name="EnrollmentId">The member's active STD or LTD enrollment the claim is against.</param>
/// <param name="Reason">Why the member is on leave.</param>
/// <param name="StartDate">First day of leave.</param>
/// <param name="EndDate">Last day of leave.</param>
public record SubmitClaimRequest(
    [Range(1, int.MaxValue)] int EnrollmentId,
    [Required, StringLength(BenefitClaim.MaxReasonLength, MinimumLength = 1)] string Reason,
    DateOnly StartDate,
    DateOnly EndDate);
