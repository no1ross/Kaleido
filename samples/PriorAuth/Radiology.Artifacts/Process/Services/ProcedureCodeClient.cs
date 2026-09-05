using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.CodeSet;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class ProcedureCodeClient(
    IKaleidoQueryableClientFactory queryableClientFactory,
    IConfiguration configuration)
{
    private readonly string procedureCodeView =
        configuration["Services:CodeSet:ProcedureCodeView"]
        ?? "ProcedureCodes";

    public async Task<ProcedureCodeRecord?> GetProcedureCodeAsync(
        string codeValue,
        ProcedureCodeSystem codeSystem,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient("CodeSet")
            .QueryContextAsync<ProcedureCodeRecord>(
                "ProcedureCodes",
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
