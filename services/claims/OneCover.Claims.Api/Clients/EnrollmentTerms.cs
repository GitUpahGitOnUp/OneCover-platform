namespace OneCover.Claims.Api.Clients;

/// <summary>
/// What Claims needs to know about one enrollment, as answered by the Enrollment service.
/// Claims keeps its own small copy of the shape (instead of referencing Enrollment's code), so the two services stay independent.
/// </summary>
// TEMPORARY SHAPE: Developer A has not built GET /internal/enrollments/{id} yet. When that endpoint exists,
// check these fields against its real response (and move the type to contracts/ if both sides agree).
/// <param name="Id">The enrollment's id.</param>
/// <param name="MemberId">The member who owns the enrollment (the Identity user id).</param>
/// <param name="PlanType">The kind of plan, for example "STD" or "LTD". Only those two can have claims.</param>
/// <param name="IsActive">True while the coverage is in force.</param>
/// <param name="CoverageStartDate">The date the coverage began. A leave can't start before it.</param>
/// <param name="BenefitPercent">The plan's benefit as a fraction, e.g. 0.60 for 60%.</param>
/// <param name="WeeklyMaximum">The most the plan pays per week.</param>
/// <param name="EliminationDays">Unpaid waiting days at the start of a leave (7 for STD, 90 for LTD).</param>
public record EnrollmentTerms(
    int Id,
    int MemberId,
    string PlanType,
    bool IsActive,
    DateOnly CoverageStartDate,
    decimal BenefitPercent,
    decimal WeeklyMaximum,
    int EliminationDays);
