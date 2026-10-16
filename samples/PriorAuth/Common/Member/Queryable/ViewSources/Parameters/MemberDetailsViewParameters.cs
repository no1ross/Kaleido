using System.ComponentModel;
using Kaleido.Queryable;
namespace Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Parameters;

public sealed record MemberDetailsViewParameters : IQueryParameters
{
    [Description("The member to get details for.")]
    public Guid? MemberId { get; init; }

    [Description("The member's enrollment (plan coverage) to get details for.")]
    public Guid? MemberEnrollmentId { get; init; }
}
