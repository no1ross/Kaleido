using System.Text.Json;
using Kaleido.Http.Queryable;
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
                        },
                        Page = new QueryApiPage { Size = 25, Offset = 0 }
                    }
                },
                cancellationToken);

        var mapping = result.Results.SingleOrDefault(x =>
            x.CodeValue.Equals(codeValue, StringComparison.OrdinalIgnoreCase));

        return mapping?.ProcessorName;
    }
}
