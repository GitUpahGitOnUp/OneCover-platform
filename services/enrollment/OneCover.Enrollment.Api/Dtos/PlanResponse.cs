using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Dtos;

/// <summary>A coverage plan and the prices available for its coverage tiers.</summary>
public sealed record PlanResponse(
    int Id,
    string Name,
    PlanType Type,
    string Description,
    decimal BenefitPercent,
    decimal WeeklyMaximum,
    int EliminationDays,
    bool IsActive,
    IReadOnlyList<PlanRateResponse> Rates);

/// <summary>A plan's monthly premium for a coverage tier.</summary>
public sealed record PlanRateResponse(CoverageTier Tier, decimal MonthlyPremium);
