using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Records
{
    public sealed record RecordExecutionContext
    (
        RecordMetadata Metadata,
        QueryRequest Request
    );
}