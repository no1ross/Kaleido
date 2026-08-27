using Kaleido.Samples.PriorAuth.Configuration.Artifacts;
using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;

public sealed class ProcedureModalityClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private readonly string modalityRuleQueryPath =
        configuration["Services:Configuration:ProcedureModalityRuleQueryPath"]
        ?? "/configuration/queryable/procedure-modality-rules/query";

    public async Task<ProcedureModality> DetermineModalityAsync(
        string codeValue,
        ProcedureCodeSystem codeSystem,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(codeValue, out var numericCode))
        {
            return ProcedureModality.Unknown;
        }

        var client =
            httpClientFactory.CreateClient("Configuration");

        using var response =
            await client.PostAsJsonAsync(
                modalityRuleQueryPath,
                QueryRequestFactory.CreateEqualsRequest(
                    ("CodeSystem", codeSystem.ToString())),
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<QueryableResult<ProcedureModalityRuleRecord>>(
                cancellationToken: cancellationToken);

        var rule =
            result?.Records.SingleOrDefault(x =>
                numericCode >= x.CodeRangeStart &&
                numericCode <= x.CodeRangeEnd);

        return rule?.Modality ?? ProcedureModality.Unknown;
    }
}
