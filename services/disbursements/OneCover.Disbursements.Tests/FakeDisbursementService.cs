using OneCover.Disbursements.Api.Dtos;
using OneCover.Disbursements.Api.Errors;
using OneCover.Disbursements.Api.Models.Enums;
using OneCover.Disbursements.Api.Services;

namespace OneCover.Disbursements.Tests;

/// <summary>
/// A stand-in for the real service, so tests can check what the controllers pass to it and how they react
/// to what it returns or throws, without a database. It records the calls it receives.
/// </summary>
public class FakeDisbursementService : IDisbursementService
{
    /// <summary>Every call to CreateForApprovedClaimAsync, in order.</summary>
    public List<(int ClaimId, int MemberId, decimal Amount, DateOnly ApprovedOn)> Created { get; } = [];

    /// <summary>What CreateForApprovedClaimAsync returns next: true = "new row", false = "already existed".</summary>
    public Queue<bool> CreateResults { get; } = new();

    /// <summary>The member id the controller passed to ListForMemberAsync / GetForMemberAsync (it must come from the token).</summary>
    public int? MemberIdSeen { get; private set; }

    /// <summary>The request body the controller passed to UpdateStatusAsync.</summary>
    public UpdateDisbursementStatusRequest? StatusRequestSeen { get; private set; }

    public Task<bool> CreateForApprovedClaimAsync(int claimId, int memberId, decimal amount, DateOnly approvedOn)
    {
        Created.Add((claimId, memberId, amount, approvedOn));
        return Task.FromResult(CreateResults.Count > 0 ? CreateResults.Dequeue() : true);
    }

    public Task<List<DisbursementDto>> ListForMemberAsync(int memberId, int page, int pageSize)
    {
        MemberIdSeen = memberId;
        return Task.FromResult(new List<DisbursementDto>());
    }

    // Always "not found", like the real service for a record that is missing or belongs to someone else.
    public Task<DisbursementDto> GetForMemberAsync(int id, int memberId)
    {
        MemberIdSeen = memberId;
        throw new NotFoundException("Disbursement", id);
    }

    public Task<List<DisbursementDto>> ListAllAsync(DisbursementStatus? status, int page, int pageSize) =>
        Task.FromResult(new List<DisbursementDto>());

    public Task<DisbursementDto> GetAsync(int id) => throw new NotFoundException("Disbursement", id);

    public Task<DisbursementDto> UpdateStatusAsync(int id, UpdateDisbursementStatusRequest request)
    {
        StatusRequestSeen = request;
        return Task.FromResult(new DisbursementDto(
            Id: id, MemberId: 1, ClaimId: 1, Amount: 100m, DueDate: new DateOnly(2026, 11, 6),
            PaidDate: null, Status: request.Status, FailureReason: request.Reason));
    }
}
