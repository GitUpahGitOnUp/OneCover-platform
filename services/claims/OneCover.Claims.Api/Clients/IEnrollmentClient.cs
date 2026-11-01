namespace OneCover.Claims.Api.Clients;

/// <summary>
/// Asks the Enrollment service about an enrollment. Claims never reads Enrollment's database (architecture rule 2),
/// so this interface is the only way Claims learns who owns an enrollment and what its plan terms are.
/// </summary>
// Hiding the call behind an interface (CLAUDE.md rule 3) means the claim service doesn't care whether the answer
// comes from a stub or from a real HTTP call, and tests can swap in a fake.
public interface IEnrollmentClient
{
    /// <summary>Gets an enrollment's owner, status and plan terms, or null if Enrollment has no such enrollment.</summary>
    /// <param name="enrollmentId">The enrollment to look up.</param>
    Task<EnrollmentTerms?> GetEnrollmentAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default);
}
