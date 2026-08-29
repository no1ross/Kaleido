using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.Parameters;
using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.ViewSources.Views;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.ViewSources;

[QueryView(
    Name = "requesting-provider-search",
    DisplayName = "Requesting Provider Search",
    Version = "1.0.0",
    Description = "Searchable requesting provider results with derived network status.",
    Visibility = QueryViewVisibility.Internal,
    DefaultSortField = nameof(RequestingProviderQueryContext.ProviderName))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class RequestingProviderSearchViewSource
    : IQueryViewSource<RequestingProviderQueryContext, RequestingProviderSearchView, RequestingProviderSearchParameters>
{
    public IQueryable<RequestingProviderSearchView> CreateView(
        IQueryable<RequestingProviderQueryContext> query,
        QueryExecutionContext executionContext)
    {
        return query
            .Select(x => new RequestingProviderSearchView
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
