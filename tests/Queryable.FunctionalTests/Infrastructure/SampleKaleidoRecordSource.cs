using Kaleido.Queryable;
using Kaleido.Shared;

namespace Kaleido.FunctionalTests.Infrastructure;

public sealed class SampleKaleidoRecordSource
    : IQueryableRecordSource<SampleKaleidoRecord>
{
    private readonly SampleKaleidoCsvData _data;

    public SampleKaleidoRecordSource(SampleKaleidoCsvData data)
    {
        _data = data;
    }

    public IQueryable<SampleKaleidoRecord> CreateQuery(RecordExecutionContext context)
    {
        return _data.Records.AsQueryable();
    }
}
