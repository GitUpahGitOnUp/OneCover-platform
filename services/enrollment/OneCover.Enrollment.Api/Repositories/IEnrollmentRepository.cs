using OneCover.Enrollment.Api.Models;

namespace OneCover.Enrollment.Api.Repositories;

public interface IEnrollmentRepository
{
    Task<IReadOnlyList<CoverageEnrollment>> GetByMemberAsync(int memberId, CancellationToken cancellationToken);
    Task<CoverageEnrollment?> GetByMemberAndIdAsync(int memberId, int id, CancellationToken cancellationToken);
    Task<CoverageEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> HasActiveEnrollmentAsync(int memberId, int planId, CancellationToken cancellationToken);
    Task AddAsync(CoverageEnrollment enrollment, CancellationToken cancellationToken);
    Task<int> CountAsync(EnrollmentStatus? status, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
