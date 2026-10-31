using OneCover.Contracts.Events;

namespace OneCover.Claims.Api.Clients;

/// <summary>
/// Tells other services about things that happened in Claims. Hiding the transport behind an interface means the
/// claim service doesn't know whether the event travels over HTTP (today) or a message queue (maybe later).
/// </summary>
public interface IClaimEventPublisher
{
    /// <summary>
    /// Sends <see cref="ClaimApproved"/> to Disbursements, retrying if it is briefly unavailable.
    /// Returns true once Disbursements accepted it, or false if every attempt failed. It never throws for a delivery
    /// problem, because the claim is already approved and saved by then.
    /// </summary>
    /// <param name="claimApproved">The event to send.</param>
    Task<bool> PublishClaimApprovedAsync(ClaimApproved claimApproved);
}
