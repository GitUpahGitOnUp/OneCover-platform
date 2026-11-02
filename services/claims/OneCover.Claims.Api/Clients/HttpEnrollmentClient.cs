using System.Net;
using System.Net.Http.Json;

namespace OneCover.Claims.Api.Clients;

public sealed class HttpEnrollmentClient(HttpClient httpClient) : IEnrollmentClient
{
    public async Task<EnrollmentTerms?> GetEnrollmentAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"internal/enrollments/{enrollmentId}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EnrollmentTerms>(
                cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Enrollment returned an empty response.");
    }
}
