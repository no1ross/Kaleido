using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.CodeSet;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class MriProcedureCodeResolverClient(
    IKaleidoQueryableClientFactory queryableClientFactory,
    IConfiguration configuration)
{
    private readonly string mriProcedureCodeRuleView =
        configuration["Services:Configuration:MriProcedureCodeRuleView"]
        ?? "MriProcedureCodeRules";

    public async Task<MriProcedureCodeRuleRecord?> ResolveAsync(
        string selectedCodeValue,
        ProcedureCodeSystem selectedCodeSystem,
        CaptureMriInfoStep processStep,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient("Configuration")
            .QueryContextAsync<MriProcedureCodeRuleRecord>(
                "MriProcedureCodeRules",
                new QueryApiRequest
                {
                    Query = new QueryBody(
                        Filter: QueryFilterNode.CreateGroup(
                            LogicalOperator.And,
                            QueryFilterNode.CreateCondition("SelectedCodeSystem", FilterOperator.Equals, selectedCodeSystem.ToString()),
                            QueryFilterNode.CreateCondition("SelectedCodeValue", FilterOperator.Equals, selectedCodeValue),
                            QueryFilterNode.CreateCondition("BodyPart", FilterOperator.Equals, processStep.BodyPart.ToString()),
                            QueryFilterNode.CreateCondition("Laterality", FilterOperator.Equals, processStep.Laterality.ToString()),
                            QueryFilterNode.CreateCondition("Contrast", FilterOperator.Equals, processStep.Contrast.ToString())))
                },
                cancellationToken);

        return result.Results.SingleOrDefault();
    }
}
