namespace OneCover.Enrollment.Api.Models;

public sealed class CoverageEnrollment
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public CoverageTier Tier { get; set; }
    public decimal MonthlyPremiumSnapshot { get; set; }
    public DateOnly EnrolledOn { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
}
