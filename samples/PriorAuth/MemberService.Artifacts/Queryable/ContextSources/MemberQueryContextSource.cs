using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.ContextSources;

internal sealed class MemberQueryContextSource(
    MemberDbContext dbContext)
    : IQueryContextSource<MemberQueryContext>
{
    public IQueryable<MemberQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.MemberEnrollments
            .AsNoTracking()
            .Select(enrollment =>
                new MemberQueryContext
                {
                    MemberEnrollmentId = enrollment.MemberEnrollmentId,
                    MemberId = enrollment.MemberId,
                    MemberNumber = enrollment.Member.MemberNumber,
                    FirstName = enrollment.Member.FirstName,
                    LastName = enrollment.Member.LastName,
                    DisplayName = enrollment.Member.FirstName + " " + enrollment.Member.LastName,
                    DateOfBirth = enrollment.Member.DateOfBirth,
                    IssuanceState = enrollment.Address.State,
                    LineOfBusiness = enrollment.LineOfBusiness,
                    PlanId = enrollment.PlanId,
                    PlanName = enrollment.PlanName
                });
    }
}
