using OneCover.Claims.Api.Dtos;

namespace OneCover.Claims.Api.Services;

/// <summary>The member-facing use cases of the Claims service. Controllers call this, never the repository.</summary>
public interface IClaimService
{
    /// <summary>
    /// Files a new claim for a member. Checks the enrollment with the Enrollment service, works out the benefit with the
    /// Identity salary and the plan terms, and saves the claim in Submitted status.
    /// Throws NotFoundException if the enrollment doesn't exist or isn't the member's, and BusinessRuleException
    /// if the enrollment isn't an active STD or LTD one or the dates break a rule.
    /// </summary>
    /// <param name="memberId">The member filing the claim (from the JWT, never the request).</param>
    /// <param name="request">The enrollment, reason and leave dates.</param>
    Task<ClaimDto> SubmitAsync(
        int memberId,
        SubmitClaimRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lists the signed-in member's own claims, newest first.</summary>
    Task<List<ClaimDto>> ListForMemberAsync(int memberId, int page, int pageSize);

    /// <summary>Gets one of the member's own claims. Throws NotFoundException if it isn't theirs or doesn't exist.</summary>
    Task<ClaimDto> GetForMemberAsync(int id, int memberId);
}
