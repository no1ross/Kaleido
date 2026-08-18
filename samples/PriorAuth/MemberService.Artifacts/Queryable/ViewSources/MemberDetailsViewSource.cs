using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.ViewSources.Views;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.ViewSources;

[QueryView(
    Name = "member-details",
    DisplayName = "Member Details",
    Version = "1.0.0",
    Description = "Detailed member enrollment information.")]
internal sealed class MemberDetailsViewSource(
    Data.MemberDbContext dbContext)
    : IQueryViewSource<
        MemberQueryContext,
        MemberDetailsView,
        MemberDetailsViewParameters>
{
    public IQueryable<MemberDetailsView> CreateView(
        IQueryable<MemberQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext.TryGetViewParameters<MemberDetailsViewParameters>()
            ?? new MemberDetailsViewParameters();

        if (parameters.MemberEnrollmentId is not null)
        {
            query = query.Where(x => x.MemberEnrollmentId == parameters.MemberEnrollmentId);
        }

        if (parameters.MemberId is not null)
        {
            query = query.Where(x => x.MemberId == parameters.MemberId);
        }

        var enrollmentIds =
            query.Select(x => x.MemberEnrollmentId);

        return dbContext.MemberEnrollments
            .AsNoTracking()
            .Include(x => x.Member)
            .Include(x => x.Address)
            .Where(x => enrollmentIds.Contains(x.MemberEnrollmentId))
            .Select(x =>
                new MemberDetailsView
                {
                    MemberId = x.MemberId,
                    MemberEnrollmentId = x.MemberEnrollmentId,
                    MemberNumber = x.Member.MemberNumber,
                    FirstName = x.Member.FirstName,
                    LastName = x.Member.LastName,
                    DisplayName = x.Member.FirstName + " " + x.Member.LastName,
                    DateOfBirth = x.Member.DateOfBirth,
                    Gender = x.Member.Gender,
                    EmailAddress = x.Member.EmailAddress,
                    PhoneNumber = x.Member.PhoneNumber,
                    PlanId = x.PlanId,
                    PlanName = x.PlanName,
                    LineOfBusiness = x.LineOfBusiness,
                    EnrollmentStatus = x.EnrollmentStatus,
                    RelationshipToSubscriber = x.RelationshipToSubscriber,
                    IssuanceState = x.Address.State,
                    AddressLine1 = x.Address.AddressLine1,
                    AddressLine2 = x.Address.AddressLine2,
                    City = x.Address.City,
                    AddressState = x.Address.State,
                    PostalCode = x.Address.PostalCode
                });
    }
}
