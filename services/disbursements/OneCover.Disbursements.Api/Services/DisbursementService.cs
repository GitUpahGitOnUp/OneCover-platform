using Microsoft.EntityFrameworkCore;
using OneCover.Disbursements.Api.Dtos;
using OneCover.Disbursements.Api.Errors;
using OneCover.Disbursements.Api.Mapping;
using OneCover.Disbursements.Api.Models;
using OneCover.Disbursements.Api.Models.Enums;
using OneCover.Disbursements.Api.Repositories;

namespace OneCover.Disbursements.Api.Services;

/// <summary>Implements the Disbursements use cases on top of the repository.</summary>
public class DisbursementService : IDisbursementService
{
    // How many days after approval a payout is due. TEMPORARY: the real rule is on the list of
    // things to agree with Developer A, so keep it in one named place that is easy to change.
    private const int DaysUntilDue = 7;

    // Page-size limits so nobody can ask for a million rows in one request.
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    private readonly IDisbursementRepository _repository;

    // TimeProvider is .NET's built-in "what time is it?" service. Asking it, instead of calling
    // DateTime.Now directly, lets tests supply a fake clock (a CLAUDE.md rule for date logic).
    private readonly TimeProvider _time;

    // Constructor injection: DI supplies both objects when it creates the service.
    public DisbursementService(IDisbursementRepository repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<bool> CreateForApprovedClaimAsync(int claimId, int memberId, decimal amount, DateOnly approvedOn)
    {
        // The entity's factory checks the inputs and throws BusinessRuleException if they're bad.
        var disbursement = Disbursement.CreatePending(
            claimId: claimId,
            memberId: memberId,
            amount: amount,
            dueDate: approvedOn.AddDays(DaysUntilDue));   // AddDays returns a NEW date, e.g. approved Mon + 7 days

        // TryAddAsync returns false when this claim already has a disbursement, so a duplicate event is harmless.
        return await _repository.TryAddAsync(disbursement);
    }

    /// <inheritdoc />
    public async Task<List<DisbursementDto>> ListForMemberAsync(int memberId, int page, int pageSize)
    {
        var (skip, take) = ToSkipAndTake(page, pageSize);

        var disbursements = await _repository.ListForMemberAsync(memberId, skip, take);

        // LINQ chain:
        //   disbursements        = the list of entities we loaded
        //   .Select(d => d.ToDto()) = for EACH entity, run ToDto() and collect the results (entity -> DTO)
        //   .ToList()            = turn the lazy sequence into a real List so it can be returned
        return disbursements.Select(d => d.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<DisbursementDto> GetForMemberAsync(int id, int memberId)
    {
        // The repository only matches rows that also belong to this member, so someone else's
        // record comes back null and we answer "not found", the same as if it didn't exist.
        var disbursement = await _repository.GetByIdForMemberAsync(id, memberId)
            ?? throw new NotFoundException("Disbursement", id);

        return disbursement.ToDto();
    }

    /// <inheritdoc />
    public async Task<List<DisbursementDto>> ListAllAsync(DisbursementStatus? status, int page, int pageSize)
    {
        var (skip, take) = ToSkipAndTake(page, pageSize);

        var disbursements = await _repository.ListAllAsync(status, skip, take);

        return disbursements.Select(d => d.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<DisbursementDto> GetAsync(int id)
    {
        var disbursement = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Disbursement", id);

        return disbursement.ToDto();
    }

    /// <inheritdoc />
    public async Task<DisbursementDto> UpdateStatusAsync(int id, UpdateDisbursementStatusRequest request)
    {
        // Load the entity WITH tracking, so EF Core notices the changes we make to it below.
        var disbursement = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Disbursement", id);

        // "switch" picks the branch that matches request.Status. The entity methods enforce the real rules
        // (only Pending can move, a reason is needed to fail), so this code only routes the request.
        switch (request.Status)
        {
            case DisbursementStatus.Paid:
                // "Today" comes from TimeProvider, never from the client. GetLocalNow() returns a
                // date AND time, so .DateTime then DateOnly.FromDateTime keeps just the date.
                disbursement.MarkPaid(DateOnly.FromDateTime(_time.GetLocalNow().DateTime));
                break;

            case DisbursementStatus.Failed:
                // "?? string.Empty" turns a missing reason (null) into "", which MarkFailed then rejects with a clear message.
                disbursement.MarkFailed(request.Reason ?? string.Empty);
                break;

            default:
                // Pending (or anything else) is not a valid target, since a disbursement starts there.
                throw new BusinessRuleException("A disbursement can only be changed to Paid or Failed.");
        }

        try
        {
            await _repository.SaveChangesAsync();
        }
        // Thrown when the RowVersion no longer matches: another admin changed this row after we loaded it.
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("This disbursement was changed by someone else. Reload it and try again.");
        }

        return disbursement.ToDto();
    }

    // Turns "page 3, 50 per page" into "skip 100 rows, take 50". Private = only this class uses it.
    private static (int Skip, int Take) ToSkipAndTake(int page, int pageSize)
    {
        // Math.Max(1, page) = never lower than 1, so page 0 or -5 acts like page 1.
        var safePage = Math.Max(1, page);

        // A missing or silly size (<= 0) falls back to the default; Math.Min caps it at the maximum.
        var safeSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        // Page 1 skips 0 rows, page 2 skips one page worth, and so on.
        return ((safePage - 1) * safeSize, safeSize);
    }
}
