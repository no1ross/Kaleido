using Kaleido.Queryable;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.CodeSet;
using Kaleido.Samples.PriorAuth.Intake.Process.Models;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

public sealed class ProcedureCodeClient(
    QueryableHttpClient queryableHttpClient,
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
        return await queryableHttpClient.QueryAsync<ProcedureCodeRecord, ProcedureCodeRecord?>(
            "CodeSet",
            procedureCodeQueryPath,
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
            result => result.Records.SingleOrDefault(
                x => string.Equals(x.CodeValue, codeValue, StringComparison.OrdinalIgnoreCase)),
            cancellationToken);
    }
}
