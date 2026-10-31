using Microsoft.EntityFrameworkCore;
using OneCover.Claims.Api.Clients;
using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Errors;
using OneCover.Claims.Api.Mapping;
using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Models.Enums;
using OneCover.Claims.Api.Repositories;
using OneCover.Contracts.Events;

namespace OneCover.Claims.Api.Services;

/// <summary>Implements the admin Claims use cases on top of the repository and the event publisher.</summary>
public class AdminClaimService : IAdminClaimService
{
    // Page-size limits so nobody can ask for a million rows in one request.
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    private readonly IClaimRepository _repository;
    private readonly IClaimEventPublisher _events;
    private readonly TimeProvider _time;
    private readonly ILogger<AdminClaimService> _logger;

    // Constructor injection: DI supplies all four objects when it creates the service.
    public AdminClaimService(
        IClaimRepository repository, IClaimEventPublisher events, TimeProvider time, ILogger<AdminClaimService> logger)
    {
        _repository = repository;
        _events = events;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ClaimDto>> ListAllAsync(ClaimStatus? status, int page, int pageSize, bool awaitingPayoutOnly = false)
    {
        var (skip, take) = ToSkipAndTake(page, pageSize);

        var claims = await _repository.ListAllAsync(status, skip, take, awaitingPayoutOnly);

        // .Select(c => c.ToDto()) = for EACH entity, build its DTO.  .ToList() = run it and return a real List.
        return claims.Select(c => c.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<ClaimDto> GetAsync(int id)
    {
        var claim = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Claim", id);

        return claim.ToDto();
    }

    /// <inheritdoc />
    public async Task<ClaimSummaryDto> GetSummaryAsync()
    {
        var counts = await _repository.CountByStatusAsync();
        var awaitingPayout = await _repository.CountAwaitingPayoutAsync();

        // LINQ chain:
        //   Enum.GetValues<ClaimStatus>()  = every status, in the order the enum declares them (the workflow order)
        //   .Select(s => new ...(s, counts.GetValueOrDefault(s))) = for each status build an entry; the database
        //                                    returns no row for a status nobody has, so GetValueOrDefault gives 0
        //   .ToList()                      = run it and return a real List
        var byStatus = Enum.GetValues<ClaimStatus>()
            .Select(s => new ClaimStatusCountDto(Status: s, Count: counts.GetValueOrDefault(s)))
            .ToList();

        return new ClaimSummaryDto(
            TotalClaims: byStatus.Sum(x => x.Count),   // .Sum(...) = add up the counts of every status
            ClaimsByStatus: byStatus,
            AwaitingPayoutCount: awaitingPayout);
    }

    /// <inheritdoc />
    public async Task<ClaimDto> UpdateStatusAsync(int id, int adminId, UpdateClaimStatusRequest request)
    {
        // Load the claim WITH tracking, so EF Core notices the changes MoveTo makes (the new status and history row).
        var claim = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Claim", id);

        // The claim itself enforces the workflow, so this code only routes the request. An invalid move throws
        // BusinessRuleException before anything changes.
        claim.MoveTo(
            newStatus: request.Status,
            changedByUserId: adminId,
            note: request.Note,
            changedAt: _time.GetUtcNow());

        try
        {
            await _repository.SaveChangesAsync();
        }
        // Thrown when the RowVersion no longer matches: another admin changed this claim after we loaded it.
        // Without this check two admins could approve and deny the same claim at the same moment.
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("This claim was changed by someone else. Reload it and try again.");
        }

        // Tell Disbursements ONLY after the approval is safely saved. If the send fails, the claim stays Approved and
        // keeps AwaitingPayout = true, which the dashboard flags. The admin still gets a success, and can use
        // "Re-send payout" later.
        if (claim.Status == ClaimStatus.Approved)
        {
            await SendClaimApprovedAsync(claim);
        }

        return claim.ToDto();
    }

    /// <inheritdoc />
    public async Task<ClaimDto> ResendPayoutAsync(int id)
    {
        var claim = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Claim", id);

        // Both checks are rules about the claim, so the entity owns them. Doing them first means a claim that was
        // never approved (or is already confirmed) is refused before we bother Disbursements.
        if (!claim.WasApproved)
        {
            throw new BusinessRuleException("Only an approved claim has a payout to send.");
        }

        if (!claim.AwaitingPayout)
        {
            throw new BusinessRuleException("The payout for this claim was already requested.");
        }

        // Unlike the approval, this request exists ONLY to send the event, so if it fails the admin must be told.
        // 503 means "try again later", which is the truth: nothing is wrong with the claim.
        if (!await SendClaimApprovedAsync(claim))
        {
            throw new ServiceUnavailableException("Disbursements could not be reached. The claim is unchanged; try again in a moment.");
        }

        return claim.ToDto();
    }

    // Sends ClaimApproved and, once Disbursements has accepted it, records that on the claim (which clears the warning).
    // Returns whether Disbursements accepted it.
    private async Task<bool> SendClaimApprovedAsync(BenefitClaim claim)
    {
        // ApprovedOn is the date the claim was REALLY approved (from its history), not today. Disbursements works out the
        // due date from it, so a re-send days later must not push the due date back.
        var approvedAt = claim.ApprovedAt ?? _time.GetUtcNow();

        var delivered = await _events.PublishClaimApprovedAsync(new ClaimApproved(
            ClaimId: claim.Id,
            MemberId: claim.MemberId,
            Amount: claim.TotalBenefit,
            ApprovedOn: DateOnly.FromDateTime(approvedAt.UtcDateTime)));

        if (!delivered)
        {
            return false;
        }

        claim.MarkPayoutRequested(_time.GetUtcNow());

        try
        {
            await _repository.SaveChangesAsync();
        }
        // The payout WAS requested, but someone changed the claim at the same instant so we couldn't record it.
        // That only leaves the warning on. A re-send is harmless (Disbursements ignores duplicates), so log it and move on.
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "ClaimApproved for claim {ClaimId} was delivered but could not be recorded on the claim.", claim.Id);
        }

        return true;
    }

    // Turns "page 3, 50 per page" into "skip 100 rows, take 50". Private = only this class uses it.
    private static (int Skip, int Take) ToSkipAndTake(int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safeSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return ((safePage - 1) * safeSize, safeSize);
    }
}
