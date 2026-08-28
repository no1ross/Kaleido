using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.ViewSources.Views;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.ViewSources;

[QueryView(
    Name = "provider-search",
    DisplayName = "Provider Search",
    Version = "1.0.0",
    Description = "Searchable provider location results.",
    DefaultSortField = nameof(ProviderLocationQueryContext.ProviderName))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class ProviderLocationSearchViewSource
    : IQueryViewSource<ProviderLocationQueryContext, ProviderLocationSearchView>
{
    public IQueryable<ProviderLocationSearchView> CreateView(
        IQueryable<ProviderLocationQueryContext> query,
        QueryExecutionContext executionContext)
    {
        return query
            .Select(x =>
                new ProviderLocationSearchView
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
                    NetworkIds = Array.Empty<Guid>(),
                    MedicalSpecialtyIds = Array.Empty<Guid>()
                });
    }
}
