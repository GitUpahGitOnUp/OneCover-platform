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
    private readonly IIdentityClient _identity;
    private readonly TimeProvider _time;
    private readonly ILogger<AdminClaimService> _logger;

    // Constructor injection: DI supplies all five objects when it creates the service.
    // IIdentityClient is how we ask the Identity service for member names.
    public AdminClaimService(
        IClaimRepository repository,
        IClaimEventPublisher events,
        IIdentityClient identity,
        TimeProvider time,
        ILogger<AdminClaimService> logger)
    {
        _repository = repository;
        _events = events;
        _identity = identity;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ClaimDto>> ListAllAsync(ClaimStatus? status, int page, int pageSize, bool awaitingPayoutOnly = false)
    {
        var (skip, take) = ToSkipAndTake(page, pageSize);

        var claims = await _repository.ListAllAsync(status, skip, take, awaitingPayoutOnly);

        // Look each member up ONCE, even if they filed several claims on this page.
        // .Select(c => c.MemberId) = from each claim keep just the member id.
        var names = await GetMemberNamesAsync(claims.Select(c => c.MemberId));

        // .Select(c => c.ToDto(...)) = for EACH entity, build its DTO and attach that member's name.
        // names.GetValueOrDefault(id) = the name if we found one, otherwise null. .ToList() = run it, return a List.
        return claims.Select(c => c.ToDto(memberName: names.GetValueOrDefault(c.MemberId))).ToList();
    }

    /// <inheritdoc />
    public async Task<ClaimDto> GetAsync(int id)
    {
        var claim = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Claim", id);

        return claim.ToDto(memberName: await TryGetMemberNameAsync(claim.MemberId));
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

        return claim.ToDto(memberName: await TryGetMemberNameAsync(claim.MemberId));
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

        return claim.ToDto(memberName: await TryGetMemberNameAsync(claim.MemberId));
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

    // Looks up the names of several members at once. Returns id -> name; a member we couldn't name is simply missing.
    private async Task<Dictionary<int, string>> GetMemberNamesAsync(IEnumerable<int> memberIds)
    {
        // .Distinct() = drop repeats, so a member with three claims is asked about once.
        // .Select(async id => ...) = start one lookup per member WITHOUT waiting, so they run side by side.
        // .ToList() = actually start them all now (LINQ is lazy and would otherwise start them one by one).
        var lookups = memberIds
            .Distinct()
            .Select(async id => (Id: id, Name: await TryGetMemberNameAsync(id)))
            .ToList();

        // Task.WhenAll = wait until every lookup has finished, and give back all the results.
        var results = await Task.WhenAll(lookups);

        // .Where(r => r.Name is not null) = keep only the members we found a name for.
        // .ToDictionary(key, value)       = build an id -> name map for quick lookup by the caller.
        return results
            .Where(r => r.Name is not null)
            .ToDictionary(r => r.Id, r => r.Name!);
    }

    // Asks Identity for one member's name. A name is only decoration, so if Identity is down or slow we log it and
    // return null rather than failing the admin's whole request.
    private async Task<string?> TryGetMemberNameAsync(int memberId)
    {
        try
        {
            var member = await _identity.GetMemberAsync(memberId);
            return member?.DisplayName;
        }
        // "when (...)" = only catch these failures: a network error, a timeout (the 5 second limit), or a bad reply.
        // Any other bug still surfaces instead of being hidden.
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            _logger.LogWarning(ex, "Could not look up the name of member {MemberId}; showing the claim without it.", memberId);
            return null;
        }
    }

    // Turns "page 3, 50 per page" into "skip 100 rows, take 50". Private = only this class uses it.
    private static (int Skip, int Take) ToSkipAndTake(int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safeSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return ((safePage - 1) * safeSize, safeSize);
    }
}
