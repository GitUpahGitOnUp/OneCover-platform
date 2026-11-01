using OneCover.Claims.Api.Clients;
using OneCover.Claims.Api.Dtos;
using OneCover.Claims.Api.Errors;
using OneCover.Claims.Api.Mapping;
using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Repositories;

namespace OneCover.Claims.Api.Services;

/// <summary>Implements the member-facing Claims use cases on top of the repository and the other services' clients.</summary>
public class ClaimService : IClaimService
{
    // Page-size limits so nobody can ask for a million rows in one request.
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    // The only plan types a disability claim can be filed against.
    private static readonly string[] ClaimablePlanTypes = ["STD", "LTD"];

    private readonly IClaimRepository _repository;
    private readonly IEnrollmentClient _enrollments;
    private readonly IIdentityClient _identity;
    private readonly IBenefitCalculator _calculator;

    // TimeProvider is .NET's built-in "what time is it?" service. Asking it, instead of calling DateTime.Now,
    // lets tests supply a fake clock (a CLAUDE.md rule for date logic).
    private readonly TimeProvider _time;

    // Constructor injection: DI supplies all five objects when it creates the service.
    public ClaimService(
        IClaimRepository repository,
        IEnrollmentClient enrollments,
        IIdentityClient identity,
        IBenefitCalculator calculator,
        TimeProvider time)
    {
        _repository = repository;
        _enrollments = enrollments;
        _identity = identity;
        _calculator = calculator;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<ClaimDto> SubmitAsync(
        int memberId,
        SubmitClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Look up the enrollment. "Doesn't exist" and "belongs to someone else" give the SAME 404, so the API
        //    never confirms that another member's enrollment exists.
        var enrollment = await _enrollments.GetEnrollmentAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null || enrollment.MemberId != memberId)
        {
            throw new NotFoundException("Enrollment", request.EnrollmentId);
        }

        // 2. Claims only go against an ACTIVE STD or LTD enrollment (CLAUDE.md domain rule).
        //    StringComparer.OrdinalIgnoreCase = compare text ignoring upper/lower case, so "std" matches "STD".
        if (!enrollment.IsActive || !ClaimablePlanTypes.Contains(enrollment.PlanType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("A claim can only be filed against an active STD or LTD enrollment.");
        }

        // 3. The calculator refuses an end date before the start date by throwing, which would surface as a 500.
        //    So check it first and give the member the proper 409. (BenefitClaim.Submit enforces the rest of the date rules.)
        if (request.EndDate < request.StartDate)
        {
            throw new BusinessRuleException("End date can't be before the start date.");
        }

        // 4. The salary lives in Identity. A valid token for a member Identity doesn't know is a data problem.
        var member = await _identity.GetMemberAsync(memberId, cancellationToken)
            ?? throw new NotFoundException("Member", memberId);

        // 5. Calculate the benefit, once, on the server. The request carries no amounts.
        var weeklyBenefit = _calculator.WeeklyBenefit(
            annualSalary: member.AnnualSalary,
            benefitPercent: enrollment.BenefitPercent,
            weeklyMaximum: enrollment.WeeklyMaximum);

        var totalBenefit = _calculator.TotalBenefit(
            weeklyBenefit: weeklyBenefit,
            startDate: request.StartDate,
            endDate: request.EndDate,
            eliminationDays: enrollment.EliminationDays);

        // 6. The entity's factory checks every remaining rule and throws BusinessRuleException if one is broken.
        //    The plan terms are copied onto the claim, so it keeps them even if the plan changes later.
        var claim = BenefitClaim.Submit(
            memberId: memberId,
            enrollmentId: enrollment.Id,
            coverageStartDate: enrollment.CoverageStartDate,
            planBenefitPercent: enrollment.BenefitPercent,
            planWeeklyMaximum: enrollment.WeeklyMaximum,
            planEliminationDays: enrollment.EliminationDays,
            reason: request.Reason,
            startDate: request.StartDate,
            endDate: request.EndDate,
            weeklyBenefit: weeklyBenefit,
            totalBenefit: totalBenefit,
            submittedAt: _time.GetUtcNow());   // "now" comes from TimeProvider, never from the client

        await _repository.AddAsync(claim);

        return claim.ToDto();
    }

    /// <inheritdoc />
    public async Task<List<ClaimDto>> ListForMemberAsync(int memberId, int page, int pageSize)
    {
        var (skip, take) = ToSkipAndTake(page, pageSize);

        var claims = await _repository.ListForMemberAsync(memberId, skip, take);

        // LINQ chain:
        //   claims                  = the list of entities we loaded
        //   .Select(c => c.ToDto()) = for EACH entity, run ToDto() and collect the results (entity -> DTO)
        //   .ToList()               = turn the lazy sequence into a real List so it can be returned
        return claims.Select(c => c.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<ClaimDto> GetForMemberAsync(int id, int memberId)
    {
        // The repository only matches claims that also belong to this member, so someone else's claim
        // comes back null and we answer "not found", the same as if it didn't exist.
        var claim = await _repository.GetByIdForMemberAsync(id, memberId)
            ?? throw new NotFoundException("Claim", id);

        return claim.ToDto();
    }

    // Turns "page 3, 50 per page" into "skip 100 rows, take 50". Private = only this class uses it.
    private static (int Skip, int Take) ToSkipAndTake(int page, int pageSize)
    {
        // Math.Max(1, page) = never lower than 1, so page 0 or -5 acts like page 1.
        var safePage = Math.Max(1, page);

        // A missing or silly size (<= 0) falls back to the default; Math.Min caps it at the maximum.
        var safeSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        return ((safePage - 1) * safeSize, safeSize);
    }
}
