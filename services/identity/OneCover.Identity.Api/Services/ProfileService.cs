using OneCover.Identity.Api.Dtos;
using OneCover.Identity.Api.Repositories;

namespace OneCover.Identity.Api.Services;

public sealed class ProfileService(
    IUserRepository users,
    ICurrentUser currentUser,
    IEligibilityService eligibilityService) : IProfileService
{
    public async Task<ProfileResponse> GetMineAsync(CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Member profile was not found.");
        return ToProfileResponse(user);
    }

    public async Task<ProfileResponse> UpdateMineAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Member profile was not found.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.SsnLast4 = request.SsnLast4;
        await users.SaveChangesAsync(cancellationToken);

        return ToProfileResponse(user);
    }

    public async Task<InternalMemberResponse> GetInternalMemberAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Member was not found.");

        return new InternalMemberResponse(
            Id: user.Id,
            Role: user.Role.ToString(),
            FirstName: user.FirstName,
            LastName: user.LastName,
            HireDate: user.HireDate,
            AnnualSalary: user.AnnualSalary,
            EmploymentType: user.EmploymentType);
    }

    private ProfileResponse ToProfileResponse(Models.User user) => new(
        Id: user.Id,
        Email: user.Email,
        Role: user.Role.ToString(),
        FirstName: user.FirstName,
        LastName: user.LastName,
        HireDate: user.HireDate,
        AnnualSalary: user.AnnualSalary,
        EmploymentType: user.EmploymentType,
        SsnLast4: user.SsnLast4,
        IsFmlaEligible: eligibilityService.IsFmlaEligible(user.HireDate, user.EmploymentType));
}
