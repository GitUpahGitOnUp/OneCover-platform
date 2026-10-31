using Microsoft.EntityFrameworkCore;
using OneCover.Claims.Api.Clients;
using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Errors;
using OneCover.Claims.Api.Mapping;
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

    // Constructor injection: DI supplies all three objects when it creates the service.
    public AdminClaimService(IClaimRepository repository, IClaimEventPublisher events, TimeProvider time)
    {
        _repository = repository;
        _events = events;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<List<ClaimDto>> ListAllAsync(ClaimStatus? status, int page, int pageSize)
    {
        var (skip, take) = ToSkipAndTake(page, pageSize);

        var claims = await _repository.ListAllAsync(status, skip, take);

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

        // Tell Disbursements ONLY after the approval is safely saved. If this send fails, the claim stays Approved
        // (the publisher logs it) and the admin still gets a success: the money side catches up later.
        if (claim.Status == ClaimStatus.Approved)
        {
            await _events.PublishClaimApprovedAsync(new ClaimApproved(
                ClaimId: claim.Id,
                MemberId: claim.MemberId,
                Amount: claim.TotalBenefit,
                ApprovedOn: DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime)));
        }

        return claim.ToDto();
    }

    // Turns "page 3, 50 per page" into "skip 100 rows, take 50". Private = only this class uses it.
    private static (int Skip, int Take) ToSkipAndTake(int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safeSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return ((safePage - 1) * safeSize, safeSize);
    }
}
