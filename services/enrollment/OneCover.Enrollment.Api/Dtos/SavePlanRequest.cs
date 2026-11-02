using System.ComponentModel.DataAnnotations;
using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Dtos;

/// <summary>Creates or replaces a plan and its full set of tier rates.</summary>
public sealed record SavePlanRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [Required, EnumDataType(typeof(PlanType))]
    public PlanType? Type { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Required, Range(typeof(decimal), "0", "1")]
    public decimal? BenefitPercent { get; init; }

    [Required, Range(typeof(decimal), "0", "100000")]
    public decimal? WeeklyMaximum { get; init; }

    [Required, Range(0, 365)]
    public int? EliminationDays { get; init; }

    [Required, MinLength(1)]
    public List<PlanRateRequest> Rates { get; init; } = [];
}

/// <summary>The monthly premium for one plan coverage tier.</summary>
public sealed record PlanRateRequest
{
    [Required, EnumDataType(typeof(CoverageTier))]
    public CoverageTier? Tier { get; init; }

    [Required, Range(typeof(decimal), "0", "10000")]
    public decimal? MonthlyPremium { get; init; }
}
