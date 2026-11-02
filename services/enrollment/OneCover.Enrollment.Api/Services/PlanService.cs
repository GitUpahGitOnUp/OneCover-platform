using OneCover.Enrollment.Api.Dtos;
using OneCover.Enrollment.Api.Models;
using OneCover.Enrollment.Api.Repositories;

namespace OneCover.Enrollment.Api.Services;

public sealed class PlanService(IPlanRepository plans) : IPlanService
{
    private static readonly CoverageTier[] AllTiers = Enum.GetValues<CoverageTier>();

    public async Task<IReadOnlyList<PlanResponse>> GetActiveAsync(CancellationToken cancellationToken) =>
        (await plans.GetActiveAsync(cancellationToken)).Select(ToResponse).ToArray();

    public async Task<PlanResponse> GetActiveByIdAsync(int id, CancellationToken cancellationToken)
    {
        var plan = await plans.GetActiveByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Active plan {id} was not found.");
        return ToResponse(plan);
    }

    public async Task<PlanResponse> CreateAsync(
        SavePlanRequest request,
        CancellationToken cancellationToken)
    {
        var plan = new Plan();
        Apply(request, plan);
        await plans.AddAsync(plan, cancellationToken);
        await plans.SaveChangesAsync(cancellationToken);
        return ToResponse(plan);
    }

    public async Task<PlanResponse> UpdateAsync(
        int id,
        SavePlanRequest request,
        CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Plan {id} was not found.");
        if (!plan.IsActive)
        {
            throw new InvalidOperationException("Retired plans cannot be edited.");
        }

        Apply(request, plan);
        await plans.SaveChangesAsync(cancellationToken);
        return ToResponse(plan);
    }

    public async Task RetireAsync(int id, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Plan {id} was not found.");
        plan.IsActive = false;
        await plans.SaveChangesAsync(cancellationToken);
    }

    private void Apply(SavePlanRequest request, Plan plan)
    {
        var type = request.Type!.Value;
        var requestedTiers = request.Rates.Select(rate => rate.Tier!.Value).ToArray();
        if (requestedTiers.Distinct().Count() != requestedTiers.Length)
        {
            throw new ArgumentException("A plan can have only one rate for each coverage tier.", nameof(request));
        }

        var isDisability = type is PlanType.STD or PlanType.LTD;
        if (isDisability && !requestedTiers.SequenceEqual([CoverageTier.EmployeeOnly]))
        {
            throw new ArgumentException("STD and LTD plans must have only the EmployeeOnly tier.", nameof(request));
        }

        if (!isDisability && !AllTiers.All(requestedTiers.Contains))
        {
            throw new ArgumentException("Every non-disability plan must define a rate for all four coverage tiers.", nameof(request));
        }

        if (isDisability && (request.BenefitPercent <= 0 || request.WeeklyMaximum <= 0))
        {
            throw new ArgumentException("STD and LTD plans require a positive benefit percentage and weekly maximum.", nameof(request));
        }

        if (!isDisability &&
            (request.BenefitPercent != 0 || request.WeeklyMaximum != 0 || request.EliminationDays != 0))
        {
            throw new ArgumentException("Non-disability plans cannot define disability benefit terms.", nameof(request));
        }

        plan.Name = request.Name.Trim();
        plan.Type = type;
        plan.Description = request.Description?.Trim() ?? string.Empty;
        plan.BenefitPercent = request.BenefitPercent!.Value;
        plan.WeeklyMaximum = request.WeeklyMaximum!.Value;
        plan.EliminationDays = request.EliminationDays!.Value;
        var rates = request.Rates.Select(rate => new PlanRate
        {
            Tier = rate.Tier!.Value,
            MonthlyPremium = rate.MonthlyPremium!.Value
        }).ToList();
        if (plan.Id != 0)
        {
            // ReplaceRates removes old rows before adding the new prices.
            plans.ReplaceRates(plan, rates);
        }
        else
        {
            plan.Rates = rates;
        }
    }

    private static PlanResponse ToResponse(Plan plan) => new(
        Id: plan.Id,
        Name: plan.Name,
        Type: plan.Type,
        Description: plan.Description,
        BenefitPercent: plan.BenefitPercent,
        WeeklyMaximum: plan.WeeklyMaximum,
        EliminationDays: plan.EliminationDays,
        IsActive: plan.IsActive,
        Rates: plan.Rates.OrderBy(rate => rate.Tier)
            .Select(rate => new PlanRateResponse(rate.Tier, rate.MonthlyPremium)).ToArray());
}
