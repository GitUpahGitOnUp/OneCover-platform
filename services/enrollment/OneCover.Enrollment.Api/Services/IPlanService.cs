using OneCover.Enrollment.Api.Dtos;

namespace OneCover.Enrollment.Api.Services;

public interface IPlanService
{
    Task<IReadOnlyList<PlanResponse>> GetActiveAsync(CancellationToken cancellationToken);
    Task<PlanResponse> GetActiveByIdAsync(int id, CancellationToken cancellationToken);
    Task<PlanResponse> CreateAsync(SavePlanRequest request, CancellationToken cancellationToken);
    Task<PlanResponse> UpdateAsync(int id, SavePlanRequest request, CancellationToken cancellationToken);
    Task RetireAsync(int id, CancellationToken cancellationToken);
}
