using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.CodeSet;
using Kaleido.Samples.PriorAuth.Intake.Process.Models;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

public sealed class ProcedureModalityClient(
    IKaleidoQueryableClientFactory queryableClientFactory,
    IConfiguration configuration)
{
    private readonly string modalityRuleView =
        configuration["Services:Configuration:ProcedureModalityRuleView"]
        ?? "ProcedureModalityRules";

    public async Task<ProcedureModality> DetermineModalityAsync(
        string codeValue,
        ProcedureCodeSystem codeSystem,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(codeValue, out var numericCode))
        {
            return ProcedureModality.Unknown;
        }

        var result = await queryableClientFactory
            .GetClient("Configuration")
            .QueryContextAsync<ProcedureModalityRuleRecord>(
                "ProcedureModalityRules",
                new QueryApiRequest
                {
                    Query = new QueryBody(
                        Filter: QueryFilterNode.CreateCondition(
                            "CodeSystem",
                            FilterOperator.Equals,
                            codeSystem.ToString()),
                        Page: new QueryPage(
                            Size: 25,
                            Offset: 0))
                },
                cancellationToken);

        var rule = result.Results.SingleOrDefault(x =>
            numericCode >= x.CodeRangeStart &&
            numericCode <= x.CodeRangeEnd);

        return rule?.Modality ?? ProcedureModality.Unknown;
    }
}
