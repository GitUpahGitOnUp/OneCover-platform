using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Repositories;

public interface IPlanRepository
{
    Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken cancellationToken);
    Task<Plan?> GetActiveByIdAsync(int id, CancellationToken cancellationToken);
    Task<Plan?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Plan plan, CancellationToken cancellationToken);
    void ReplaceRates(Plan plan, IReadOnlyCollection<PlanRate> rates);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
