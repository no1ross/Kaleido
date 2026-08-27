using System.Net.Http.Json;
using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;

public sealed class ProcedureCodeClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private readonly string procedureCodeQueryPath =
        configuration["Services:CodeSet:ProcedureCodeQueryPath"]
        ?? "/code-set/queryable/procedure-codes/query";

    public async Task<ProcedureCodeRecord?> GetProcedureCodeAsync(
        string codeValue,
        ProcedureCodeSystem codeSystem,
        CancellationToken cancellationToken = default)
    {
        var client =
            httpClientFactory.CreateClient("CodeSet");

        using var response =
            await client.PostAsJsonAsync(
                procedureCodeQueryPath,
                QueryRequestFactory.CreateEqualsRequest(
                    ("CodeValue", codeValue),
                    ("CodeSystem", codeSystem.ToString())),
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<QueryableResult<ProcedureCodeRecord>>(
                cancellationToken: cancellationToken);

        return result?.Records.SingleOrDefault();
    }
}
