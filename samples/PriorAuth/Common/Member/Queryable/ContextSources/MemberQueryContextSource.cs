using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Member.Data;
using Kaleido.Samples.PriorAuth.Member.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Member.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Members",
    Description = "Member enrollments, searchable by member number and name.",
    Source = "Prior Authorization Member Service")]
internal sealed class MemberQueryContextSource(
    MemberDbContext dbContext)
    : IQuerySource<MemberQueryContext>
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
                    PlanName = enrollment.PlanName,
                    EffectiveDate = enrollment.EffectiveDate,
                    TerminationDate = enrollment.TerminationDate
                });
    }
}
