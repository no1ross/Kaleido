using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable
{
    public sealed record RecordExecutionContext
    (
        RecordMetadata Metadata,
        KaleidoQueryRequest Request
    );
}