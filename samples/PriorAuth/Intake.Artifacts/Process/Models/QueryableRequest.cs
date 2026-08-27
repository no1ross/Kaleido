namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

public sealed record QueryRequest<TParameters>
{
    public TParameters? Parameters { get; init; }

    public QueryBody? Query { get; init; }
}

public sealed record QueryBody
{
    public string? SearchText { get; init; }

    public QueryFilterNode? Filter { get; init; }
}

public sealed record QueryFilterNode
{
    public QueryFilterCondition? Condition { get; init; }

    public QueryFilterGroup? Group { get; init; }
}

public sealed record QueryFilterCondition
{
    public string Field { get; init; } = string.Empty;

    public string Operator { get; init; } = "Equals";

    public IReadOnlyCollection<object> Values { get; init; } = [];
}

public sealed record QueryFilterGroup
{
    public string Operator { get; init; } = "And";

    public IReadOnlyCollection<QueryFilterNode> Filters { get; init; } = [];
}

public static class QueryRequestFactory
{
    public static QueryRequest<object> CreateEqualsRequest(
        params (string Field, object Value)[] filters)
    {
        return new QueryRequest<object>
        {
            Query = new QueryBody
            {
                Filter = new QueryFilterNode
                {
                    Group = new QueryFilterGroup
                    {
                        Filters = filters
                            .Select(filter => new QueryFilterNode
                            {
                                Condition = new QueryFilterCondition
                                {
                                    Field = filter.Field,
                                    Values = [filter.Value]
                                }
                            })
                            .ToArray()
                    }
                }
            }
        };
    }
}
