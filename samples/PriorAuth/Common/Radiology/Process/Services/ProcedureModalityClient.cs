using System.Text.Json;
using Kaleido.Http.Queryable;
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
            .QuerySourceAsync<ProcedureModalityRuleQueryContext>(
                "ProcedureModalityRuleQueryContextSource",
                new QueryApiRequest
                {
                    Query = new QueryApiBody
                    {
                        Filter = new QueryApiFilterNode
                        {
                            Condition = new QueryApiFilterCondition
                            {
                                Field = "CodeSystem",
                                Operator = "equals",
                                Values = [JsonSerializer.SerializeToElement(codeSystem.ToString())]
                            }
                        }
                    }
                },
                cancellationToken);

        var rule = result.Results.SingleOrDefault(x =>
            numericCode >= x.CodeRangeStart &&
            numericCode <= x.CodeRangeEnd);

        return rule?.Modality ?? ProcedureModality.Unknown;
    }
}
