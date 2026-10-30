namespace OneCover.Claims.Api.Services;

/// <summary>
/// The disability benefit math. Pure functions: the same inputs always give the same outputs,
/// with no database, no clock and no state, so one shared instance is safe (register it as a singleton).
/// </summary>
public class BenefitCalculator : IBenefitCalculator
{
    /// <inheritdoc />
    // Example: $78,000 salary, 60% plan, $1,500 weekly max
    //   78000 / 52 = 1500 a week -> 1500 x 0.60 = 900 -> under the 1,500 cap -> 900.00
    // Non-disability plans have a 0 percent and a 0 maximum, so this returns 0 for them.
    public decimal WeeklyBenefit(decimal annualSalary, decimal benefitPercent, decimal weeklyMaximum)
    {
        // 52 weeks in a year. The "m" suffix makes the number a decimal (exact for money) instead of a double.
        var weeklySalary = annualSalary / 52m;

        // Math.Round(x, 2) = round to 2 decimal places (cents).
        var benefit = Math.Round(weeklySalary * benefitPercent, 2);

        // Math.Min = take the smaller of the two, so the benefit never exceeds the plan's weekly cap.
        return Math.Min(benefit, weeklyMaximum);
    }

    /// <inheritdoc />
    // Example: $700/week, leave Oct 1 -> Oct 14 (14 days counting both ends), 7-day elimination period
    //   14 - 7 = 7 payable days -> 700 / 7 = 100 a day -> 7 x 100 = 700.00
    public decimal TotalBenefit(decimal weeklyBenefit, DateOnly startDate, DateOnly endDate, int eliminationDays)
    {
        // DayNumber = how many days since 0001-01-01, so subtracting two gives the gap in days.
        // The "+ 1" counts both the first and the last day of the leave.
        var totalDays = endDate.DayNumber - startDate.DayNumber + 1;

        // The waiting period is unpaid. Math.Max(0, ...) stops a short leave from going negative.
        var payableDays = Math.Max(0, totalDays - eliminationDays);

        // Daily rate (weekly / 7) times the paid days, rounded to cents.
        return Math.Round(weeklyBenefit / 7m * payableDays, 2);
    }
}
