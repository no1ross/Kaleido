using Kaleido.Http.Abstractions.Queryable.Contracts;
using Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Views;
using Microsoft.Extensions.Configuration;
using Kaleido.Http.Abstractions.Queryable;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

public sealed class MemberDetailsClient(
    IKaleidoQueryableClientFactory queryableClientFactory)
{
    public async Task<MemberDetailsView?> GetMemberDetailsAsync(
        Guid memberId,
        Guid memberEnrollmentId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient("Member")
            .QueryViewAsync<MemberDetailsViewParameters, MemberDetailsView>(
                "members",
                "member-details",
                new QueryApiRequest<MemberDetailsViewParameters>
                {
                    Parameters = new MemberDetailsViewParameters
                    {
                        MemberId = memberId,
                        MemberEnrollmentId = memberEnrollmentId
                    }
                },
                cancellationToken);

        return result.Results.SingleOrDefault();
    }
}
