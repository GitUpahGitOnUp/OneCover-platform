using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Dtos;

/// <summary>A member's enrollment with its snapshotted premium and estimated paycheck deduction.</summary>
public sealed record EnrollmentResponse(
    int Id,
    int PlanId,
    string PlanName,
    PlanType PlanType,
    CoverageTier Tier,
    decimal MonthlyPremium,
    decimal EstimatedPerPaycheckDeduction,
    DateOnly EnrolledOn,
    EnrollmentStatus Status);
