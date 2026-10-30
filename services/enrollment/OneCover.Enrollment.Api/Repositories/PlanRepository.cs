using Microsoft.EntityFrameworkCore;
using OneCover.Enrollment.Api.Data;
using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Repositories;

public sealed class PlanRepository(EnrollmentDbContext db) : IPlanRepository
{
    public async Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken cancellationToken) =>
        await db.Plans.AsNoTracking().Where(plan => plan.IsActive)
            .Include(plan => plan.Rates).OrderBy(plan => plan.Name)
            .ToListAsync(cancellationToken);

    public Task<Plan?> GetActiveByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Plans.Where(plan => plan.IsActive)
            .Include(plan => plan.Rates)
            .SingleOrDefaultAsync(plan => plan.Id == id, cancellationToken);

    public Task<Plan?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Plans.Include(plan => plan.Rates).SingleOrDefaultAsync(plan => plan.Id == id, cancellationToken);

    public Task AddAsync(Plan plan, CancellationToken cancellationToken) =>
        db.Plans.AddAsync(plan, cancellationToken).AsTask();

    public void ReplaceRates(Plan plan, IReadOnlyCollection<PlanRate> rates)
    {
        db.PlanRates.RemoveRange(plan.Rates);
        foreach (var rate in rates)
        {
            rate.PlanId = plan.Id;
        }

        plan.Rates = rates.ToList();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
