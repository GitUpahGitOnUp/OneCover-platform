using OneCover.Claims.Api.Services;

namespace OneCover.Claims.Tests;

/// <summary>Unit tests for the disability benefit math: the weekly cap, the elimination period and partial periods.</summary>
// Pure functions are the easiest code to test: same inputs give the same outputs, nothing to fake.
// Each test name says the rule it proves: Method_Result_WhenCondition.
// [Fact] marks a method as one test. Each test follows Arrange (set up) / Act (call) / Assert (check).
public class BenefitCalculatorTests
{
    // One calculator shared by every test in this class. It has no state, so sharing it is safe.
    private readonly BenefitCalculator _calc = new();

    [Fact]
    public void WeeklyBenefit_IsSixtyPercentOfWeeklySalary_WhenUnderTheCap()
    {
        // 78,000 / 52 = 1,500 a week x 60% = 900
        var result = _calc.WeeklyBenefit(annualSalary: 78_000m, benefitPercent: 0.60m, weeklyMaximum: 1_500m);

        Assert.Equal(900m, result);
    }

    [Fact]
    public void WeeklyBenefit_IsCappedAtWeeklyMaximum()
    {
        // 200,000 / 52 x 60% = 2,307.69, but the plan caps it at 1,500
        var result = _calc.WeeklyBenefit(annualSalary: 200_000m, benefitPercent: 0.60m, weeklyMaximum: 1_500m);

        Assert.Equal(1_500m, result);
    }

    [Fact]
    public void WeeklyBenefit_IsRoundedToCents()
    {
        // 50,000 / 52 = 961.538... x 60% = 576.923..., which rounds to 576.92
        var result = _calc.WeeklyBenefit(annualSalary: 50_000m, benefitPercent: 0.60m, weeklyMaximum: 1_500m);

        Assert.Equal(576.92m, result);
    }

    [Fact]
    public void WeeklyBenefit_IsZero_ForNonDisabilityPlans()
    {
        // Plans that aren't STD/LTD have a 0 percent and a 0 max
        var result = _calc.WeeklyBenefit(annualSalary: 78_000m, benefitPercent: 0m, weeklyMaximum: 0m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void TotalBenefit_IsZero_WhenLeaveIsShorterThanEliminationPeriod()
    {
        var start = new DateOnly(2026, 10, 1);

        // Oct 1-6 = 6 days, all inside the 7-day waiting period
        var result = _calc.TotalBenefit(700m, start, start.AddDays(5), eliminationDays: 7);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void TotalBenefit_IsZero_WhenLeaveIsExactlyTheEliminationPeriod()
    {
        var start = new DateOnly(2026, 10, 1);

        // Oct 1-7 = 7 days, and all 7 are unpaid
        var result = _calc.TotalBenefit(700m, start, start.AddDays(6), eliminationDays: 7);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void TotalBenefit_PaysOnlyTheDaysAfterTheEliminationPeriod()
    {
        var start = new DateOnly(2026, 10, 1);

        // Oct 1-14 = 14 days - 7 waiting = 7 paid days x (700 / 7 = 100 a day) = 700
        var result = _calc.TotalBenefit(700m, start, start.AddDays(13), eliminationDays: 7);

        Assert.Equal(700m, result);
    }

    [Fact]
    public void TotalBenefit_ProratesAPartialWeek()
    {
        var start = new DateOnly(2026, 10, 1);

        // Oct 1-10 = 10 days - 7 waiting = 3 paid days x 100 = 300
        var result = _calc.TotalBenefit(700m, start, start.AddDays(9), eliminationDays: 7);

        Assert.Equal(300m, result);
    }

    [Fact]
    public void TotalBenefit_CountsBothTheStartAndEndDay()
    {
        var day = new DateOnly(2026, 10, 1);

        // A one-day leave (start = end) with no waiting period = 1 paid day = 100
        var result = _calc.TotalBenefit(700m, day, day, eliminationDays: 0);

        Assert.Equal(100m, result);
    }

    [Fact]
    public void TotalBenefit_UsesTheNinetyDayEliminationPeriodForLtd()
    {
        var start = new DateOnly(2026, 1, 1);

        // 100 days of leave - 90 unpaid = 10 paid days x 100 a day = 1,000
        var result = _calc.TotalBenefit(700m, start, start.AddDays(99), eliminationDays: 90);

        Assert.Equal(1_000m, result);
    }
}
