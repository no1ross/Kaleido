using Kaleido.Queryable.Records;
using Kaleido.Queryable.Testing;

namespace Kaleido.Queryable.FunctionalTests.Infrastructure;

public sealed class SampleKaleidoRecordSource
    : IRecordSource<SampleKaleidoRecord>
{
    private readonly SampleKaleidoCsvData _data;

    public SampleKaleidoRecordSource(
        SampleKaleidoCsvData data)
    {
        _data = data;
    }

    public IQueryable<SampleKaleidoRecord> CreateQuery(
        RecordExecutionContext executionContext)
    {
        return _data.Records.AsQueryable();
    }
}