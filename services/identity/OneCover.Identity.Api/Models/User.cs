namespace OneCover.Identity.Api.Models;

public sealed class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Member;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly HireDate { get; set; }
    public decimal AnnualSalary { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public string SsnLast4 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
