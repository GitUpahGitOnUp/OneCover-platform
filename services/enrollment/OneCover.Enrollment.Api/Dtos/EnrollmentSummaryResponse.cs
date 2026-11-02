namespace OneCover.Enrollment.Api.Dtos;

/// <summary>Counts of all, active, and cancelled enrollments.</summary>
public sealed record EnrollmentSummaryResponse(int Total, int Active, int Cancelled);
