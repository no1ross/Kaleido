using System.Text.Json;
using Kaleido.Http.Queryable;
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
            .QuerySourceAsync<MriProcedureCodeRuleQueryContext>(
                "MriProcedureCodeRuleQueryContextSource",
                new QueryApiRequest
                {
                    Query = new QueryApiBody
                    {
                        Filter = new QueryApiFilterNode
                        {
                            Group = new QueryApiFilterGroup
                            {
                                Operator = "and",
                                Filters =
                                [
                                    new QueryApiFilterNode
                                    {
                                        Condition = new QueryApiFilterCondition
                                        {
                                            Field = "SelectedCodeSystem",
                                            Operator = "equals",
                                            Values = [JsonSerializer.SerializeToElement(selectedCodeSystem.ToString())]
                                        }
                                    },
                                    new QueryApiFilterNode
                                    {
                                        Condition = new QueryApiFilterCondition
                                        {
                                            Field = "SelectedCodeValue",
                                            Operator = "equals",
                                            Values = [JsonSerializer.SerializeToElement(selectedCodeValue)]
                                        }
                                    },
                                    new QueryApiFilterNode
                                    {
                                        Condition = new QueryApiFilterCondition
                                        {
                                            Field = "BodyPart",
                                            Operator = "equals",
                                            Values = [JsonSerializer.SerializeToElement(processStep.BodyPart.ToString())]
                                        }
                                    },
                                    new QueryApiFilterNode
                                    {
                                        Condition = new QueryApiFilterCondition
                                        {
                                            Field = "Laterality",
                                            Operator = "equals",
                                            Values = [JsonSerializer.SerializeToElement(processStep.Laterality.ToString())]
                                        }
                                    },
                                    new QueryApiFilterNode
                                    {
                                        Condition = new QueryApiFilterCondition
                                        {
                                            Field = "Contrast",
                                            Operator = "equals",
                                            Values = [JsonSerializer.SerializeToElement(processStep.Contrast.ToString())]
                                        }
                                    }
                                ]
                            }
                        }
                    }
                },
                cancellationToken);

        return result.Results.SingleOrDefault();
    }
}
