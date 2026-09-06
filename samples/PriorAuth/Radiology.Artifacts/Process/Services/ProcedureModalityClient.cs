using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.CodeSet;
using Kaleido.Samples.PriorAuth.Configuration;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class ProcedureModalityClient(
    IKaleidoQueryableClientFactory queryableClientFactory)
{
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
            .QueryContextAsync<ProcedureModalityRuleQueryContext>(
                "procedure-modality-rules",
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
