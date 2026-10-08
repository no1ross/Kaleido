using System.Text.Json;
using Kaleido.Http.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class MriProcedureCodeResolverClient(
    IKaleidoQueryableClientFactory queryableClientFactory)
{
    public async Task<MriProcedureCodeRuleQueryContext?> ResolveAsync(
        string selectedCodeValue,
        ProcedureCodeSystem selectedCodeSystem,
        MriBodyPart bodyPart,
        Laterality laterality,
        ContrastOption contrast,
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
                                            Values = [JsonSerializer.SerializeToElement(bodyPart.ToString())]
                                        }
                                    },
                                    new QueryApiFilterNode
                                    {
                                        Condition = new QueryApiFilterCondition
                                        {
                                            Field = "Laterality",
                                            Operator = "equals",
                                            Values = [JsonSerializer.SerializeToElement(laterality.ToString())]
                                        }
                                    },
                                    new QueryApiFilterNode
                                    {
                                        Condition = new QueryApiFilterCondition
                                        {
                                            Field = "Contrast",
                                            Operator = "equals",
                                            Values = [JsonSerializer.SerializeToElement(contrast.ToString())]
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
