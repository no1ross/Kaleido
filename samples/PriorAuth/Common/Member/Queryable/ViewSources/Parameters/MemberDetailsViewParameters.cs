using Kaleido.Queryable;
namespace Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Parameters;

public sealed record MemberDetailsViewParameters : IQueryParameters
{
    public Guid? MemberId { get; init; }

    public Guid? MemberEnrollmentId { get; init; }
}
