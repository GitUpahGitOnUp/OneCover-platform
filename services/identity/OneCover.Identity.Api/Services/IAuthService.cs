using OneCover.Identity.Api.Dtos;

namespace OneCover.Identity.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
