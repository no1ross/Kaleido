using Kaleido.Queryable;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Configuration;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

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
                new QueryRequest(
                new QueryBody(
                    SearchText: codeValue,
                    Filter: QueryFilterNode.CreateCondition(
                        "CodeSystem",
                        FilterOperator.Equals,
                        codeSystem.ToString()),
                    Page: new QueryPage(
                        Size: 25,
                        Offset: 0))),
                result => result,
                cancellationToken))
            .Records.SingleOrDefault(x =>
                numericCode >= x.CodeRangeStart &&
                numericCode <= x.CodeRangeEnd);

        return rule?.Modality ?? ProcedureModality.Unknown;
    }
}
