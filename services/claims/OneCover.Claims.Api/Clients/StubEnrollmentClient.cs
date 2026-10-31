namespace OneCover.Claims.Api.Clients;

/// <summary>
/// A TEMPORARY stand-in for the Enrollment service, used until Developer A builds GET /internal/enrollments/{id}.
/// It returns the same canned STD plan for every enrollment id. Replace it with a real HTTP client then.
/// </summary>
public class StubEnrollmentClient : IEnrollmentClient
{
    /// <inheritdoc />
    // The stub treats enrollment N as owned by member N, so a developer can try "my enrollment" (same number)
    // and "someone else's enrollment" (different numbers) without a real Enrollment service.
    // Task.FromResult wraps a ready answer in a Task, since the real client will be async but this one has nothing to wait for.
    public Task<EnrollmentTerms?> GetEnrollmentAsync(int enrollmentId) =>
        Task.FromResult<EnrollmentTerms?>(new EnrollmentTerms(
            Id: enrollmentId,
            MemberId: enrollmentId,
            PlanType: "STD",
            IsActive: true,
            CoverageStartDate: new DateOnly(2026, 1, 1),
            BenefitPercent: 0.60m,
            WeeklyMaximum: 1_500m,
            EliminationDays: 7));
}
