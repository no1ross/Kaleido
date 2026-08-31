using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Configuration.Artifacts;
using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;

public sealed class ProcedureModalityClient(
    QueryableHttpClient queryableHttpClient,
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

        var rule =
            (await queryableHttpClient.QueryAsync<ProcedureModalityRuleRecord, QueryResult<ProcedureModalityRuleRecord>>(
                "Configuration",
                modalityRuleQueryPath,
                QueryRequestFactory.CreateEqualsRequest(
                    ("CodeSystem", codeSystem.ToString())),
                result => result,
                cancellationToken))
            .Records.SingleOrDefault(x =>
                numericCode >= x.CodeRangeStart &&
                numericCode <= x.CodeRangeEnd);

        return rule?.Modality ?? ProcedureModality.Unknown;
    }
}
