using Kaleido.Queryable.Exceptions;
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

        throw new InvalidParameterTypeException(
            "ViewParameters",
            typeof(TViewParameters),
            Request.ViewParameters.GetType());
    }
}