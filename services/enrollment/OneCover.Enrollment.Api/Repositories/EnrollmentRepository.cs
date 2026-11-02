using Microsoft.EntityFrameworkCore;
using OneCover.Enrollment.Api.Data;
using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Repositories;

public sealed class EnrollmentRepository(EnrollmentDbContext db) : IEnrollmentRepository
{
    public async Task<IReadOnlyList<CoverageEnrollment>> GetByMemberAsync(
        int memberId,
        CancellationToken cancellationToken) =>
        await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.MemberId == memberId)
            .Include(enrollment => enrollment.Plan)
            .OrderByDescending(enrollment => enrollment.EnrolledOn)
            .ToListAsync(cancellationToken);

    public Task<CoverageEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Enrollments.AsNoTracking().Include(enrollment => enrollment.Plan)
            .SingleOrDefaultAsync(enrollment => enrollment.Id == id, cancellationToken);

    public Task<CoverageEnrollment?> GetByMemberAndIdAsync(
        int memberId,
        int id,
        CancellationToken cancellationToken) =>
        db.Enrollments.AsNoTracking().Include(enrollment => enrollment.Plan)
            .SingleOrDefaultAsync(enrollment =>
                enrollment.Id == id && enrollment.MemberId == memberId, cancellationToken);

    public Task<bool> HasActiveEnrollmentAsync(
        int memberId,
        int planId,
        CancellationToken cancellationToken) =>
        db.Enrollments.AnyAsync(enrollment =>
            enrollment.MemberId == memberId &&
            enrollment.PlanId == planId &&
            enrollment.Status == EnrollmentStatus.Active, cancellationToken);

    public Task AddAsync(CoverageEnrollment enrollment, CancellationToken cancellationToken) =>
        db.Enrollments.AddAsync(enrollment, cancellationToken).AsTask();

    public Task<int> CountAsync(EnrollmentStatus? status, CancellationToken cancellationToken)
    {
        var query = db.Enrollments.AsNoTracking();
        if (status.HasValue)
        {
            query = query.Where(enrollment => enrollment.Status == status.Value);
        }

        return query.CountAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
