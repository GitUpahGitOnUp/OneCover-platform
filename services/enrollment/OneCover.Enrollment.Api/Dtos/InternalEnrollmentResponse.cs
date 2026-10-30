using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Dtos;

/// <summary>Enrollment ownership and plan terms returned to internal services.</summary>
public sealed record InternalEnrollmentResponse(
    int Id,
    int MemberId,
    int PlanId,
    EnrollmentStatus Status,
    PlanType PlanType,
    decimal BenefitPercent,
    decimal WeeklyMaximum,
    int EliminationDays);
