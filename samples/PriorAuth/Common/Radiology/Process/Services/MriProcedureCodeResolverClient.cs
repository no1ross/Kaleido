using Kaleido.Http.Queryable;
using Kaleido.Http.Queryable;
using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class MriProcedureCodeResolverClient(
    IKaleidoQueryableClientFactory queryableClientFactory)
{
    public async Task<MriProcedureCodeRuleQueryContext?> ResolveAsync(
        string selectedCodeValue,
        ProcedureCodeSystem selectedCodeSystem,
        CaptureMriInfoStep processStep,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient("Configuration")
            .QueryContextAsync<MriProcedureCodeRuleQueryContext>(
                "mri-procedure-code-rules",
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
