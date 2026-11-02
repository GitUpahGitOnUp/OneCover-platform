using OneCover.Identity.Api.Dtos;

namespace OneCover.Identity.Api.Services;

public interface IProfileService
{
    Task<ProfileResponse> GetMineAsync(CancellationToken cancellationToken);
    Task<ProfileResponse> UpdateMineAsync(UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<InternalMemberResponse> GetInternalMemberAsync(int id, CancellationToken cancellationToken);
}
