using OneCover.Disbursements.Api.Dtos;
using OneCover.Disbursements.Api.Models;

namespace OneCover.Disbursements.Api.Mapping;

/// <summary>Converts Disbursement entities into DisbursementDto objects for API responses.</summary>
// A "static class" can't be instantiated. It only holds helper methods.
public static class DisbursementMappings
{
    /// <summary>Builds the DTO the API returns for a disbursement.</summary>
    // "this Disbursement d" makes this an EXTENSION METHOD: you can write disbursement.ToDto()
    // as if ToDto were a method on Disbursement itself.
    public static DisbursementDto ToDto(this Disbursement d) => new(
        // NAMED arguments (Id: ...) are required by our CLAUDE.md. If the DTO's fields are ever
        // reordered, a positional call could silently put the wrong value in the wrong field.
        Id: d.Id,
        MemberId: d.MemberId,
        ClaimId: d.ClaimId,
        Amount: d.Amount,
        DueDate: d.DueDate,
        PaidDate: d.PaidDate,
        Status: d.Status,
        FailureReason: d.FailureReason);
}
