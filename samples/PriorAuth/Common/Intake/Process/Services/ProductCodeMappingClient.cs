using Kaleido.Queryable;
using Kaleido.Queryable.Http.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

public sealed class ProductCodeMappingClient(
    IKaleidoQueryableClientFactory queryableClientFactory)
{
    public async Task<string?> GetProcessorNameAsync(
        string codeValue,
        ProcedureCodeSystem codeSystem,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient("Configuration")
            .QueryContextAsync<ProductCodeMappingQueryContext>(
                "product-code-mappings",
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

        var mapping = result.Results.SingleOrDefault(x =>
            x.CodeValue.Equals(codeValue, StringComparison.OrdinalIgnoreCase));

        return mapping?.ProcessorName;
    }
}
