using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable
{
    public sealed record RecordExecutionContext
    (
        RuntimeRecordMetadata Metadata,
        KaleidoQueryRequest Request
    );
}