using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Api.Dtos;

/// <summary>
/// What the API returns for one claim, including its full status history (oldest first). Never exposes the entity.
/// </summary>
// "record" = a class built to carry data. The values in ( ) become read-only properties and the compiler writes
// the constructor. The <param> tags document each property, since a record declares them inside the ( ).
// Unlike the monolith's ClaimDto there is no member name or plan name: those belong to the Identity and
// Enrollment services, so this service only returns their ids and the web app can look the names up.
/// <param name="Id">The claim's id.</param>
/// <param name="MemberId">The member who filed the claim (the Identity user id).</param>
/// <param name="EnrollmentId">The STD or LTD enrollment the claim is against.</param>
/// <param name="Reason">Why the member is on leave.</param>
/// <param name="StartDate">First day of leave.</param>
/// <param name="EndDate">Last day of leave.</param>
/// <param name="Status">Where the claim is in the review workflow.</param>
/// <param name="PlanBenefitPercent">The plan's benefit fraction when the claim was filed, e.g. 0.60 for 60%.</param>
/// <param name="PlanWeeklyMaximum">The plan's weekly maximum when the claim was filed.</param>
/// <param name="PlanEliminationDays">The plan's unpaid waiting days when the claim was filed.</param>
/// <param name="WeeklyBenefit">Weekly benefit, calculated once by the server at filing.</param>
/// <param name="TotalBenefit">Total benefit for the leave after the unpaid waiting days, calculated once at filing.</param>
/// <param name="SubmittedAt">When the claim was filed.</param>
/// <param name="NextStatuses">The statuses the workflow allows next. The admin "Move to..." choices.</param>
/// <param name="History">The audit trail of status changes, oldest first.</param>
public record ClaimDto(
    int Id,
    int MemberId,
    int EnrollmentId,
    string Reason,
    DateOnly StartDate,
    DateOnly EndDate,
    ClaimStatus Status,
    decimal PlanBenefitPercent,
    decimal PlanWeeklyMaximum,
    int PlanEliminationDays,
    decimal WeeklyBenefit,
    decimal TotalBenefit,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<ClaimStatus> NextStatuses,
    IReadOnlyList<ClaimHistoryDto> History);
