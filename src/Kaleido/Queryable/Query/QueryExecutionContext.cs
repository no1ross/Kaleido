using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

[ExcludeFromCodeCoverage]
public sealed record QueryExecutionContext
(
    QuerySourceMetadata Metadata,
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

        throw new KaleidoValidationException(
            QueryableErrorCodes.InvalidParameterValue,
            $"ViewParameters is of type '{Request.ViewParameters.GetType().Name}' but expected '{typeof(TViewParameters).Name}'.");
    }
}