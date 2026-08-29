using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Queryable.ContextSources;

internal sealed class RequestingProviderSearchQueryContextSource(
    RequestingProviderSearchClient requestingProviderSearchClient)
    : IQueryContextSource<RequestingProviderSearchQueryContext>
{
    public IQueryable<RequestingProviderSearchQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext.TryGetViewParameters<RequestingProviderSearchQueryParameters>()
            ?? throw new InvalidOperationException("RequestingProviderSearchQueryParameters are required.");

        var result =
            requestingProviderSearchClient.SearchAsync(
                    parameters.ProcessId,
                    executionContext.Request.Query)
                .GetAwaiter()
                .GetResult();

        return result.Records
            .Select(record => new RequestingProviderSearchQueryContext
            {
                ProviderLocationId = record.ProviderLocationId,
                ProviderId = record.ProviderId,
                ProviderName = record.ProviderName,
                LocationName = record.LocationName,
                StateCode = record.StateCode,
                PostalCode = record.PostalCode,
                City = record.City,
                PhoneNumber = record.PhoneNumber,
                PrimaryTin = record.PrimaryTin,
                PrimaryNpi = record.PrimaryNpi,
                PrimaryMedicalSpecialtyId = record.PrimaryMedicalSpecialtyId,
                PrimaryMedicalSpecialtyName = record.PrimaryMedicalSpecialtyName,
                PrimaryMedicalSpecialtyCode = record.PrimaryMedicalSpecialtyCode,
                IsInNetwork = record.IsInNetwork
            })
            .AsQueryable();
    }
}
