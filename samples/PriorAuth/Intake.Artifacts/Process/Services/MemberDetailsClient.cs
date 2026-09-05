using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Samples.PriorAuth.Intake.Process.Models;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

public sealed class MemberDetailsClient(
    IKaleidoQueryableClientFactory queryableClientFactory,
    IConfiguration configuration)
{
    private readonly string memberDetailsView =
        configuration["Services:MemberService:MemberDetailsView"]
        ?? "MemberDetails";

    public async Task<MemberDetailsRecord?> GetMemberDetailsAsync(
        Guid memberId,
        Guid memberEnrollmentId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient("MemberService")
            .QueryViewAsync<MemberDetailsQueryParameters, MemberDetailsRecord>(
                "Members",
                memberDetailsView,
                new QueryApiRequest<MemberDetailsQueryParameters>
                {
                    Parameters = new MemberDetailsQueryParameters
                    {
                        MemberId = memberId,
                        MemberEnrollmentId = memberEnrollmentId
                    }
                },
                cancellationToken);

        return result.Results.SingleOrDefault();
    }
}
