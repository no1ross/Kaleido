using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Queryable.ViewSources;

[QueryView(
    Name = "requesting-provider-search",
    DisplayName = "Requesting Provider Search",
    Version = "1.0.0",
    Description = "Searchable requesting provider results resolved through Intake.",
    DefaultSortField = nameof(RequestingProviderSearchQueryContext.ProviderName))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class RequestingProviderSearchViewSource
    : IQueryViewSource<RequestingProviderSearchQueryContext, RequestingProviderSearchRecord, RequestingProviderSearchQueryParameters>
{
    public IQueryable<RequestingProviderSearchRecord> CreateView(
        IQueryable<RequestingProviderSearchQueryContext> query,
        QueryExecutionContext executionContext)
    {
        return query
            .Select(x => new RequestingProviderSearchRecord
            {
                ProviderLocationId = x.ProviderLocationId,
                ProviderId = x.ProviderId,
                ProviderName = x.ProviderName,
                LocationName = x.LocationName,
                City = x.City,
                StateCode = x.StateCode,
                PostalCode = x.PostalCode,
                PhoneNumber = x.PhoneNumber,
                PrimaryTin = x.PrimaryTin,
                PrimaryNpi = x.PrimaryNpi,
                PrimaryMedicalSpecialtyId = x.PrimaryMedicalSpecialtyId,
                PrimaryMedicalSpecialtyName = x.PrimaryMedicalSpecialtyName,
                PrimaryMedicalSpecialtyCode = x.PrimaryMedicalSpecialtyCode,
                IsInNetwork = x.IsInNetwork
            });
    }
}
