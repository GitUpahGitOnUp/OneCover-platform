using OneCover.Identity.Api.Models;

namespace OneCover.Identity.Api.Services;

public sealed class EligibilityService(TimeProvider timeProvider) : IEligibilityService
{
    private const int MinimumFmlaHours = 1250;

    public bool IsFmlaEligible(DateOnly hireDate, EmploymentType employmentType)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var weeksInYear = 52;
        var weeklyHours = employmentType switch
        {
            EmploymentType.FullTime => 40,
            EmploymentType.PartTime => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(employmentType))
        };
        var estimatedHours = weeklyHours * weeksInYear;

        return hireDate.AddMonths(12) <= today && estimatedHours >= MinimumFmlaHours;
    }
}
