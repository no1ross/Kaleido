using Kaleido.Queryable;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.CodeSet;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class MriProcedureCodeResolverClient(
    QueryableHttpClient queryableHttpClient,
    IConfiguration configuration)
{
    private readonly string mriProcedureCodeRuleQueryPath =
        configuration["Services:Configuration:MriProcedureCodeRuleQueryPath"]
        ?? "/configuration/queryable/mri-procedure-code-rules/query";

    public async Task<MriProcedureCodeRuleRecord?> ResolveAsync(
        string selectedCodeValue,
        ProcedureCodeSystem selectedCodeSystem,
        CaptureMriInfoStep processStep,
        CancellationToken cancellationToken = default)
    {
        return await queryableHttpClient.QueryAsync<MriProcedureCodeRuleRecord, MriProcedureCodeRuleRecord?>(
            "Configuration",
            mriProcedureCodeRuleQueryPath,
            new QueryRequest(
                new QueryBody(
                    Filter: QueryFilterNode.CreateGroup(
                        LogicalOperator.And,
                        QueryFilterNode.CreateCondition("SelectedCodeSystem", FilterOperator.Equals, selectedCodeSystem.ToString()),
                        QueryFilterNode.CreateCondition("SelectedCodeValue", FilterOperator.Equals, selectedCodeValue),
                        QueryFilterNode.CreateCondition("BodyPart", FilterOperator.Equals, processStep.BodyPart.ToString()),
                        QueryFilterNode.CreateCondition("Laterality", FilterOperator.Equals, processStep.Laterality.ToString()),
                        QueryFilterNode.CreateCondition("Contrast", FilterOperator.Equals, processStep.Contrast.ToString())))),
            result => result.Records.SingleOrDefault(),
            cancellationToken);
    }
}
