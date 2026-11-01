namespace OneCover.Enrollment.Api.Dtos;

/// <summary>Enrollment ownership and plan terms returned to internal services.</summary>
public sealed record InternalEnrollmentResponse(
    int Id,
    int MemberId,
    string PlanType,
    bool IsActive,
    DateOnly CoverageStartDate,
    decimal BenefitPercent,
    decimal WeeklyMaximum,
    int EliminationDays);
