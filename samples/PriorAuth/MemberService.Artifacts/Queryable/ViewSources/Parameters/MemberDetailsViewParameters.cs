namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.ViewSources.Parameters;

public sealed record MemberDetailsViewParameters
{
    public Guid? MemberId { get; init; }

    public Guid? MemberEnrollmentId { get; init; }
}
