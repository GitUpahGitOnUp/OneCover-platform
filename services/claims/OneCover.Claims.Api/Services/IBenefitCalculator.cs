namespace OneCover.Claims.Api.Services;

/// <summary>
/// Calculates STD/LTD disability benefits from the member's salary, the plan's terms and the claim's dates.
/// Used once, on the server, when a claim is filed. The request never carries an amount.
/// </summary>
// An interface only declares what a class can do, not how. The claim service will depend on this
// interface, so tests (or a future LTD calculator) can swap in a different implementation.
public interface IBenefitCalculator
{
    /// <summary>Weekly benefit: annual salary / 52 x benefit percent, capped at the plan's weekly maximum.</summary>
    /// <param name="annualSalary">The member's annual salary (from Identity).</param>
    /// <param name="benefitPercent">The plan's benefit as a fraction, e.g. 0.60 for 60%.</param>
    /// <param name="weeklyMaximum">The most the plan pays per week.</param>
    decimal WeeklyBenefit(decimal annualSalary, decimal benefitPercent, decimal weeklyMaximum);

    /// <summary>
    /// Total benefit for the leave: (weekly / 7) x payable days. The first <paramref name="eliminationDays"/>
    /// days are unpaid (7 for STD, 90 for LTD). Start and end dates are both counted.
    /// Returns 0 if the leave is no longer than the elimination period.
    /// </summary>
    /// <param name="weeklyBenefit">The result of <see cref="WeeklyBenefit"/>.</param>
    /// <param name="startDate">First day of leave.</param>
    /// <param name="endDate">Last day of leave.</param>
    /// <param name="eliminationDays">Unpaid waiting days at the start of the leave.</param>
    decimal TotalBenefit(decimal weeklyBenefit, DateOnly startDate, DateOnly endDate, int eliminationDays);
}
