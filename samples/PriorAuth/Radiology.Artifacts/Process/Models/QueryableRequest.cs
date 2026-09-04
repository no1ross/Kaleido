//using Kaleido.Queryable;
//using Kaleido.Queryable.Query;

//namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

//public sealed record QueryApiRequest<TParameters>
//    where TParameters : class
//{
//    public TParameters? Parameters { get; init; }

//    public QueryBody? Query { get; init; }
//}

//public static class QueryRequestFactory
//{
//    public static QueryRequest CreateEqualsRequest(
//        params (string Field, object Value)[] filters)
//    {
//        return new QueryRequest(
//            new QueryBody(
//                Filter: QueryFilterNode.CreateGroup(
//                    LogicalOperator.And,
//                    filters
//                        .Select(filter =>
//                            QueryFilterNode.CreateCondition(
//                                filter.Field,
//                                FilterOperator.Equals,
//                                filter.Value))
//                        .ToArray())));
//    }
//}
