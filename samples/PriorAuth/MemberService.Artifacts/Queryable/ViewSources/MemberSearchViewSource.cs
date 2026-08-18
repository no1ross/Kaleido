using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.ViewSources.Views;

namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.ViewSources;

[QueryView(
    Name = "member-search",
    DisplayName = "Member Search",
    Version = "1.0.0",
    Description = "Searchable member enrollment results.",
    DefaultSortField = nameof(MemberQueryContext.LastName))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class MemberSearchViewSource
    : IQueryViewSource<MemberQueryContext, MemberSearchView>
{
    public IQueryable<MemberSearchView> CreateView(
        IQueryable<MemberQueryContext> query,
        QueryExecutionContext executionContext)
    {
        return query
            .Select(x =>
                new MemberSearchView
                {
                    MemberId = x.MemberId,
                    MemberEnrollmentId = x.MemberEnrollmentId,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    DateOfBirth = x.DateOfBirth,
                    DisplayName = x.DisplayName,
                    MemberNumber = x.MemberNumber,
                    IssuanceState = x.IssuanceState,
                    LineOfBusiness = x.LineOfBusiness,
                    PlanId = x.PlanId,
                    PlanName = x.PlanName
                });
    }
}
