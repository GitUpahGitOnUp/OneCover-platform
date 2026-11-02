namespace OneCover.Enrollment.Api.Models;

public sealed class Plan
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlanType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal BenefitPercent { get; set; }
    public decimal WeeklyMaximum { get; set; }
    public int EliminationDays { get; set; }
    public bool IsActive { get; set; } = true;
    public List<PlanRate> Rates { get; set; } = [];

    public bool IsDisability => Type is PlanType.STD or PlanType.LTD;
}
