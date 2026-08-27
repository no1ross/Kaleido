using System.Net.Http.Json;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;

public sealed class MemberDetailsClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private readonly string memberDetailsQueryPath =
        configuration["Services:MemberService:MemberDetailsQueryPath"]
        ?? "/member/queryable/members/member-details/query";

    public async Task<MemberDetailsRecord?> GetMemberDetailsAsync(
        Guid memberId,
        Guid memberEnrollmentId,
        CancellationToken cancellationToken = default)
    {
        var client =
            httpClientFactory.CreateClient("MemberService");

        using var response =
            await client.PostAsJsonAsync(
                memberDetailsQueryPath,
                new MemberDetailsQueryRequest
                {
                    Parameters = new MemberDetailsQueryParameters
                    {
                        MemberId = memberId,
                        MemberEnrollmentId = memberEnrollmentId
                    }
                },
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<QueryableResult<MemberDetailsRecord>>(
                cancellationToken: cancellationToken);

        return result?.Records.SingleOrDefault();
    }
}
