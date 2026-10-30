using OneCover.Enrollment.Api.Dtos;

namespace OneCover.Enrollment.Api.Services;

public interface IEnrollmentService
{
    Task<IReadOnlyList<EnrollmentResponse>> GetMineAsync(CancellationToken cancellationToken);
    Task<EnrollmentResponse> GetMineByIdAsync(int id, CancellationToken cancellationToken);
    Task<EnrollmentResponse> EnrollAsync(EnrollRequest request, CancellationToken cancellationToken);
    Task<EnrollmentSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);
    Task<InternalEnrollmentResponse> GetInternalByIdAsync(int id, CancellationToken cancellationToken);
}
