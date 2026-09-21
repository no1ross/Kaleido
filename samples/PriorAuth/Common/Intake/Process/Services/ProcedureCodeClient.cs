using Kaleido.Queryable;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.CodeSet;
using Kaleido.Samples.PriorAuth.CodeSet.Queryable.Contexts;
using Kaleido.Http.Queryable.Contracts;
using Kaleido.Http.Queryable;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

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
