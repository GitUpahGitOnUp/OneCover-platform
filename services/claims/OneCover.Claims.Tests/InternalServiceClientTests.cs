using System.Net;
using System.Text;
using OneCover.Claims.Api.Clients;

namespace OneCover.Claims.Tests;

public sealed class InternalServiceClientTests
{
    [Fact]
    public async Task EnrollmentClient_DeserializesTheInternalContract()
    {
        using var client = CreateClient(
            "http://enrollment/",
            """{"id":7,"memberId":42,"planType":"STD","isActive":true,"coverageStartDate":"2026-01-15","benefitPercent":0.60,"weeklyMaximum":1500,"eliminationDays":7}""",
            HttpStatusCode.OK,
            request => Assert.Equal("http://enrollment/internal/enrollments/7", request.RequestUri?.AbsoluteUri));

        var terms = await new HttpEnrollmentClient(client).GetEnrollmentAsync(7);

        Assert.NotNull(terms);
        Assert.Equal(42, terms.MemberId);
        Assert.Equal(new DateOnly(2026, 1, 15), terms.CoverageStartDate);
        Assert.Equal(0.60m, terms.BenefitPercent);
    }

    [Fact]
    public async Task IdentityClient_DeserializesAnnualSalaryAsDecimal()
    {
        using var client = CreateClient(
            "http://identity/",
            """{"id":42,"role":"Member","firstName":"Casey","lastName":"Member","hireDate":"2020-01-15","annualSalary":78000,"employmentType":"FullTime"}""",
            HttpStatusCode.OK,
            request => Assert.Equal("http://identity/internal/members/42", request.RequestUri?.AbsoluteUri));

        var member = await new HttpIdentityClient(client).GetMemberAsync(42);

        Assert.NotNull(member);
        Assert.Equal(42, member.Id);
        Assert.Equal(78_000m, member.AnnualSalary);
    }

    [Fact]
    public async Task InternalClients_ReturnNullOnlyWhenTheServiceReturnsNotFound()
    {
        using var enrollmentHttp = CreateClient("http://enrollment/", "", HttpStatusCode.NotFound, _ => { });
        using var identityHttp = CreateClient("http://identity/", "", HttpStatusCode.NotFound, _ => { });

        Assert.Null(await new HttpEnrollmentClient(enrollmentHttp).GetEnrollmentAsync(404));
        Assert.Null(await new HttpIdentityClient(identityHttp).GetMemberAsync(404));
    }

    [Fact]
    public async Task InternalClients_ThrowForOtherUnsuccessfulResponses()
    {
        using var enrollmentHttp = CreateClient("http://enrollment/", "", HttpStatusCode.ServiceUnavailable, _ => { });
        using var identityHttp = CreateClient("http://identity/", "", HttpStatusCode.ServiceUnavailable, _ => { });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => new HttpEnrollmentClient(enrollmentHttp).GetEnrollmentAsync(7));
        await Assert.ThrowsAsync<HttpRequestException>(
            () => new HttpIdentityClient(identityHttp).GetMemberAsync(42));
    }

    private static HttpClient CreateClient(
        string baseAddress,
        string body,
        HttpStatusCode statusCode,
        Action<HttpRequestMessage> assertRequest)
    {
        var handler = new ResponseHandler(body, statusCode, assertRequest);
        return new HttpClient(handler)
        {
            BaseAddress = new Uri(baseAddress)
        };
    }

    private sealed class ResponseHandler(
        string body,
        HttpStatusCode statusCode,
        Action<HttpRequestMessage> assertRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            assertRequest(request);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
