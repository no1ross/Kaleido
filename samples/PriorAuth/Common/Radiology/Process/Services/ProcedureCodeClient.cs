using Kaleido.Http.Queryable;
using Kaleido.Http.Queryable.Contracts;
using Kaleido.Queryable;
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
            .QueryContextAsync<ProcedureCodeQueryContext>(
                "procedure-codes",
                new QueryApiRequest
                {
                    Query = new QueryBody(
                        SearchText: codeValue,
                        Filter: QueryFilterNode.CreateCondition(
                            "CodeSystem",
                            FilterOperator.Equals,
                            codeSystem.ToString()),
                        Page: new QueryPage(
                            Size: 25,
                            Offset: 0))
                },
                cancellationToken);

        return result.Results.SingleOrDefault(
            x => string.Equals(x.CodeValue, codeValue, StringComparison.OrdinalIgnoreCase));
    }
}
