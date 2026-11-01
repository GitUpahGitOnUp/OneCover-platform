using System.Net;
using System.Net.Http.Json;

namespace OneCover.Claims.Api.Clients;

public sealed class HttpIdentityClient(HttpClient httpClient) : IIdentityClient
{
    public async Task<MemberInfo?> GetMemberAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"internal/members/{memberId}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MemberInfo>(
                cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Identity returned an empty response.");
    }
}
