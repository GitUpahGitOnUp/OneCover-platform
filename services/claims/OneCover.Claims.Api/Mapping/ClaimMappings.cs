using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Models;

namespace OneCover.Claims.Api.Mapping;

/// <summary>Converts BenefitClaim entities (and their history rows) into DTOs for API responses.</summary>
// A "static class" can't be instantiated. It only holds helper methods.
public static class ClaimMappings
{
    /// <summary>Builds the DTO the API returns for a claim, including its history. The claim must have its History loaded.</summary>
    // "this BenefitClaim c" makes this an EXTENSION METHOD: you can write claim.ToDto() as if it belonged to BenefitClaim.
    public static ClaimDto ToDto(this BenefitClaim c) => new(
        // NAMED arguments are required by CLAUDE.md. This DTO has many fields of the same type (decimals, ints),
        // so a positional call could silently put a value in the wrong field if the DTO is ever reordered.
        Id: c.Id,
        MemberId: c.MemberId,
        EnrollmentId: c.EnrollmentId,
        Reason: c.Reason,
        StartDate: c.StartDate,
        EndDate: c.EndDate,
        Status: c.Status,
        PlanBenefitPercent: c.PlanBenefitPercent,
        PlanWeeklyMaximum: c.PlanWeeklyMaximum,
        PlanEliminationDays: c.PlanEliminationDays,
        WeeklyBenefit: c.WeeklyBenefit,
        TotalBenefit: c.TotalBenefit,
        SubmittedAt: c.SubmittedAt,
        // Ask the workflow rulebook which moves are allowed from the current status.
        NextStatuses: ClaimWorkflow.NextStatuses(c.Status),
        // LINQ chain over the claim's history:
        //   .OrderBy(h => h.ChangedAt) = sort the rows oldest first, so the timeline reads top to bottom
        //   .ThenBy(h => h.Id)         = if two rows share a time, the earlier-saved row (smaller id) goes first
        //   .Select(h => h.ToDto())    = for EACH row, run ToDto() and collect the results (entity -> DTO)
        //   .ToList()                  = turn the lazy sequence into a real List so it can be returned
        History: c.History
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => h.ToDto())
            .ToList());

    /// <summary>Builds the DTO the API returns for one history row.</summary>
    public static ClaimHistoryDto ToDto(this ClaimStatusHistory h) => new(
        FromStatus: h.FromStatus,
        ToStatus: h.ToStatus,
        ChangedByUserId: h.ChangedByUserId,
        ChangedAt: h.ChangedAt,
        Note: h.Note);
}
