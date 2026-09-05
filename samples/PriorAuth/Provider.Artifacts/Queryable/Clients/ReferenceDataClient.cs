using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Provider.Queryable.Models;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Samples.PriorAuth.Provider.Queryable.Clients;

public sealed class ReferenceDataClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private readonly string planQueryPath =
        configuration["Services:ReferenceData:PlanQueryPath"]
        ?? "/reference-data/queryable/plans/query";

    public IReadOnlySet<Guid> GetNetworkIdsByPlanId(
        string planId,
        CancellationToken cancellationToken = default)
    {
        using var response =
            httpClientFactory
                .CreateClient("ReferenceData")
                .PostAsJsonAsync(
                    planQueryPath,
                    new QueryApiRequest(
                        Query: new QueryBody(
                            SearchText: planId)),
                    cancellationToken)
                .GetAwaiter()
                .GetResult();

        if (response.IsSuccessStatusCode)
        {
            var result =
                response.Content
                    .ReadFromJsonAsync<QueryableResult<PlanNetworkRecord>>(
                        cancellationToken: cancellationToken)
                    .GetAwaiter()
                    .GetResult();

            return result?.Results
                .SelectMany(x => x.NetworkIds)
                .ToHashSet()
                ?? new HashSet<Guid>();
        }

        var queryableError =
            response.StatusCode == HttpStatusCode.BadRequest
                ? response.Content
                    .ReadFromJsonAsync<QueryableErrorResponse>(
                        cancellationToken: cancellationToken)
                    .GetAwaiter()
                    .GetResult()
                : null;

        if (queryableError?.Errors.Count > 0)
        {
            throw new InvalidOperationException(
                string.Join(" ", queryableError.Errors.Select(error => error.Message)));
        }

        throw new InvalidOperationException(
            $"ReferenceData plan query failed with status code {(int)response.StatusCode} ({response.StatusCode}).");
    }
}
