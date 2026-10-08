using System.Text.Json;
using Kaleido.Http.Queryable;
using Kaleido.Samples.PriorAuth.CodeSet.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class ProcedureCodeClient(
    IKaleidoQueryableClientFactory queryableClientFactory)
{
    public async Task<ProcedureCodeQueryContext?> GetProcedureCodeAsync(
        string codeValue,
        ProcedureCodeSystem codeSystem,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient("CodeSet")
            .QuerySourceAsync<ProcedureCodeQueryContext>(
                "ProcedureCodeQueryContextSource",
                new QueryApiRequest
                {
                    Query = new QueryApiBody
                    {
                        SearchText = codeValue,
                        Filter = new QueryApiFilterNode
                        {
                            Condition = new QueryApiFilterCondition
                            {
                                Field = "CodeSystem",
                                Operator = "equals",
                                Values = [JsonSerializer.SerializeToElement(codeSystem.ToString())]
                            }
                        },
                        Page = new QueryApiPage { Size = 25, Offset = 0 }
                    }
                },
                cancellationToken);

        return result.Results.SingleOrDefault(
            x => string.Equals(x.CodeValue, codeValue, StringComparison.OrdinalIgnoreCase));
    }
}
