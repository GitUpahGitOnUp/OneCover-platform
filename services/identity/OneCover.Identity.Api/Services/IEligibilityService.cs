using OneCover.Identity.Api.Models;

namespace OneCover.Identity.Api.Services;

public interface IEligibilityService
{
    bool IsFmlaEligible(DateOnly hireDate, EmploymentType employmentType);
}
