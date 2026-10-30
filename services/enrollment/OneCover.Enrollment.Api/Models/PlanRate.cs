namespace OneCover.Enrollment.Api.Models;

public sealed class PlanRate
{
    public int PlanId { get; set; }
    public CoverageTier Tier { get; set; }
    public decimal MonthlyPremium { get; set; }
    public Plan Plan { get; set; } = null!;
}
