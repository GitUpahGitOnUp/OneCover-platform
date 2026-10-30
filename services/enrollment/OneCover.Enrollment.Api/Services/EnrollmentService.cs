using OneCover.Enrollment.Api.Dtos;
using OneCover.Enrollment.Api.Models;
using OneCover.Enrollment.Api.Repositories;

namespace OneCover.Enrollment.Api.Services;

public sealed class EnrollmentService(
    IEnrollmentRepository enrollments,
    IPlanRepository plans,
    ICurrentMember currentMember,
    TimeProvider timeProvider) : IEnrollmentService
{
    private const int PayPeriodsPerYear = 26;

    public async Task<IReadOnlyList<EnrollmentResponse>> GetMineAsync(CancellationToken cancellationToken)
    {
        var items = await enrollments.GetByMemberAsync(currentMember.MemberId, cancellationToken);
        return items.Select(ToResponse).ToArray();
    }

    public async Task<EnrollmentResponse> GetMineByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var enrollment = await enrollments.GetByMemberAndIdAsync(
            currentMember.MemberId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Enrollment {id} was not found.");
        return ToResponse(enrollment);
    }

    public async Task<EnrollmentResponse> EnrollAsync(
        EnrollRequest request,
        CancellationToken cancellationToken)
    {
        var plan = await plans.GetActiveByIdAsync(request.PlanId!.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Active plan {request.PlanId} was not found.");
        var tier = request.Tier!.Value;
        var rate = plan.Rates.SingleOrDefault(item => item.Tier == tier)
            ?? throw new ArgumentException("The selected coverage tier is not available for this plan.", nameof(request));

        if (await enrollments.HasActiveEnrollmentAsync(currentMember.MemberId, plan.Id, cancellationToken))
        {
            throw new InvalidOperationException("You are already enrolled in this plan.");
        }

        var enrollment = new CoverageEnrollment
        {
            MemberId = currentMember.MemberId,
            Plan = plan,
            Tier = tier,
            MonthlyPremiumSnapshot = rate.MonthlyPremium,
            EnrolledOn = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)
        };
        await enrollments.AddAsync(enrollment, cancellationToken);
        await enrollments.SaveChangesAsync(cancellationToken);
        return ToResponse(enrollment);
    }

    public async Task<EnrollmentSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var total = await enrollments.CountAsync(null, cancellationToken);
        var active = await enrollments.CountAsync(EnrollmentStatus.Active, cancellationToken);
        return new EnrollmentSummaryResponse(
            Total: total,
            Active: active,
            Cancelled: total - active);
    }

    public async Task<InternalEnrollmentResponse> GetInternalByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var enrollment = await enrollments.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Enrollment {id} was not found.");
        return new InternalEnrollmentResponse(
            Id: enrollment.Id,
            MemberId: enrollment.MemberId,
            PlanId: enrollment.PlanId,
            Status: enrollment.Status,
            PlanType: enrollment.Plan.Type,
            BenefitPercent: enrollment.Plan.BenefitPercent,
            WeeklyMaximum: enrollment.Plan.WeeklyMaximum,
            EliminationDays: enrollment.Plan.EliminationDays);
    }

    private static EnrollmentResponse ToResponse(CoverageEnrollment enrollment) => new(
        Id: enrollment.Id,
        PlanId: enrollment.PlanId,
        PlanName: enrollment.Plan.Name,
        PlanType: enrollment.Plan.Type,
        Tier: enrollment.Tier,
        MonthlyPremium: enrollment.MonthlyPremiumSnapshot,
        EstimatedPerPaycheckDeduction:
            decimal.Round(enrollment.MonthlyPremiumSnapshot * 12m / PayPeriodsPerYear, 2),
        EnrolledOn: enrollment.EnrolledOn,
        Status: enrollment.Status);
}
