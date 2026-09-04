namespace Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Parameters;

public sealed record MemberDetailsViewParameters
{
    public Guid? MemberId { get; init; }

    public Guid? MemberEnrollmentId { get; init; }
}
