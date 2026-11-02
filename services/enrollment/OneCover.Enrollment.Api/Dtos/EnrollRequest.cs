using System.ComponentModel.DataAnnotations;
using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Dtos;

/// <summary>Selects an active plan and an available coverage tier for enrollment.</summary>
public sealed record EnrollRequest
{
    [Required, Range(1, int.MaxValue)]
    public int? PlanId { get; init; }

    [Required, EnumDataType(typeof(CoverageTier))]
    public CoverageTier? Tier { get; init; }
}
