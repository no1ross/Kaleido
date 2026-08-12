using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

public sealed record QueryExecutionContext
(
    QueryContextMetadata Metadata,
    IQueryRequest Request
)
{
    public TViewParameters? TryGetViewParameters<TViewParameters>()
        where TViewParameters : class
    {
        if (Request.ViewParameters is null)
        {
            return null;
        }

        if (Request.ViewParameters is TViewParameters parameters)
        {
            return parameters;
        }

        throw new InvalidOperationException(
            $"Query view parameters were provided as '{Request.ViewParameters.GetType().Name}', " +
            $"but '{typeof(TViewParameters).Name}' was expected.");
    }
}