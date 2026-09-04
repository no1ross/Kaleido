using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Samples.PriorAuth.Intake.Process.Models;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

public sealed class MemberDetailsClient(
    QueryableHttpClient queryableHttpClient,
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
        return await queryableHttpClient.QueryAsync<MemberDetailsQueryParameters, MemberDetailsRecord, MemberDetailsRecord?>(
            "MemberService",
            memberDetailsQueryPath,
            new QueryApiRequest<MemberDetailsQueryParameters>
            {
                Parameters = new MemberDetailsQueryParameters
                {
                    MemberId = memberId,
                    MemberEnrollmentId = memberEnrollmentId
                }
            },
            result => result.Records.SingleOrDefault(),
            cancellationToken);
    }
}
