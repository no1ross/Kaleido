using System.Net;
using System.Net.Http.Json;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Models;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

public sealed class QueryableHttpClient(
    IHttpClientFactory httpClientFactory)
{
    public async Task<TResult> QueryAsync<TRecord, TResult>(
        string clientName,
        string requestPath,
        QueryRequest request,
        Func<QueryResult<TRecord>, TResult> selector,
        CancellationToken cancellationToken = default)
        where TRecord : class
    {
        using var response =
            await httpClientFactory
                .CreateClient(clientName)
                .PostAsJsonAsync(
                    requestPath,
                    request,
                    cancellationToken);

        var result =
            await ReadResultAsync<TRecord>(response, cancellationToken);

        return selector(result);
    }

    public async Task<TResult> QueryAsync<TParameters, TRecord, TResult>(
        string clientName,
        string requestPath,
        QueryApiRequest<TParameters> request,
        Func<QueryResult<TRecord>, TResult> selector,
        CancellationToken cancellationToken = default)
        where TParameters : class
        where TRecord : class
    {
        using var response =
            await httpClientFactory
                .CreateClient(clientName)
                .PostAsJsonAsync(
                    requestPath,
                    request,
                    cancellationToken);

        var result =
            await ReadResultAsync<TRecord>(response, cancellationToken);

        return selector(result);
    }

    private static async Task<QueryResult<TRecord>> ReadResultAsync<TRecord>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where TRecord : class
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<QueryResult<TRecord>>(
                       cancellationToken: cancellationToken)
                   ?? throw new QueryableClientException(
                       "Queryable request succeeded but returned no payload.",
                       response.StatusCode);
        }

        var queryableError =
            response.StatusCode == HttpStatusCode.BadRequest
                ? await response.Content.ReadFromJsonAsync<QueryableErrorResponse>(
                    cancellationToken: cancellationToken)
                : null;

        if (queryableError?.Errors.Count > 0)
        {
            throw new QueryableClientException(
                string.Join(" ", queryableError.Errors.Select(error => error.Message)),
                response.StatusCode,
                queryableError.Errors);
        }

        throw new QueryableClientException(
            $"Queryable request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }
}
