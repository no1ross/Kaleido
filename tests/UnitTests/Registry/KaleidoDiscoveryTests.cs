using Kaleido.Attributes;
using Kaleido.Queryable;
using Kaleido.Registry;
using Xunit;

namespace Kaleido.UnitTests.Registry;

public sealed class KaleidoDiscoveryTests
{
    [Fact]
    public void Scan_Should_Discover_Record_Types()
    {
        var result = KaleidoDiscovery.Scan(
        [
            typeof(TestRecord).Assembly
        ]);

        var record = Assert.Single(
            result.Records,
            x => x.RecordType == typeof(TestRecord));

        Assert.Equal(
            typeof(TestRecord),
            record.RecordType);
    }

    [Fact]
    public void Scan_Should_Discover_Record_Metadata()
    {
        var result = KaleidoDiscovery.Scan(
        [
            typeof(TestRecord).Assembly
        ]);

        var record = Assert.Single(
            result.Records,
            x => x.RecordType == typeof(TestRecord));

        Assert.Equal(
            "test-record",
            record.Metadata.Name);

        Assert.Equal(
            "1.0.0",
            record.Metadata.Version);

        Assert.Equal(
            "Unit Test",
            record.Metadata.Source);

        Assert.Single(
            record.Metadata.Fields);

        Assert.Equal(
            nameof(TestRecord.Id),
            record.Metadata.Fields[0].Name);

        Assert.Equal(
            typeof(int),
            record.Metadata.Fields[0].FieldType);
    }

    [Fact]
    public void Scan_Should_Discover_Source_Types()
    {
        var result = KaleidoDiscovery.Scan(
        [
            typeof(TestRecord).Assembly
        ]);

        var source = Assert.Single(
            result.Sources,
            x => x.ImplementationType == typeof(TestRecordSource));

        Assert.Equal(
            typeof(TestRecord),
            source.RecordType);
    }

    [Fact]
    public void Scan_Should_Discover_Named_Query_Types()
    {
        var result = KaleidoDiscovery.Scan(
        [
            typeof(TestRecord).Assembly
        ]);

        var query = Assert.Single(
            result.NamedQueries,
            x => x.ImplementationType == typeof(TestNamedQuery));

        Assert.Equal(
            typeof(TestRecord),
            query.RecordType);
    }

    [Fact]
    public void Scan_Should_Discover_Multiple_Named_Queries_For_A_Record()
    {
        var result = KaleidoDiscovery.Scan(
        [
            typeof(TestRecord).Assembly
        ]);

        var queries = result.NamedQueries
            .Where(x => x.RecordType == typeof(TestRecord))
            .ToList();

        Assert.Equal(2, queries.Count);
    }

    [Fact]
    public void Scan_Should_Ignore_Abstract_Sources()
    {
        var result = KaleidoDiscovery.Scan(
        [
            typeof(TestRecord).Assembly
        ]);

        Assert.DoesNotContain(
            result.Sources,
            x => x.ImplementationType == typeof(AbstractSource));
    }

    [Fact]
    public void Scan_Should_Ignore_Classes_Without_Record_Attribute()
    {
        var result = KaleidoDiscovery.Scan(
        [
            typeof(TestRecord).Assembly
        ]);

        Assert.DoesNotContain(
            result.Records,
            x => x.RecordType == typeof(NotARecord));
    }

    [Fact]
    public void Scan_Should_Not_Return_Duplicate_Records_When_Assembly_Is_Supplied_Twice()
    {
        var assembly = typeof(TestRecord).Assembly;

        var result = KaleidoDiscovery.Scan(
        [
            assembly,
            assembly
        ]);

        Assert.Single(
            result.Records,
            x => x.RecordType == typeof(TestRecord));
    }

    [KaleidoRecord(
        "test-record",
        "1.0.0",
        "Unit Test")]
    private sealed class TestRecord
    {
        public int Id { get; init; }
    }

    private sealed class NotARecord
    {
        public int Id { get; init; }
    }

    private abstract class AbstractSource :
        IQueryableRecordSource<TestRecord>
    {
        public abstract IQueryable<TestRecord> CreateQuery(
            RecordExecutionContext context);
    }

    private sealed class TestRecordSource :
        IQueryableRecordSource<TestRecord>
    {
        public IQueryable<TestRecord> CreateQuery(
            RecordExecutionContext context)
        {
            return Enumerable.Empty<TestRecord>()
                .AsQueryable();
        }
    }

    private sealed class TestNamedQuery :
        IQueryableRecordNamedQuery<TestRecord>
    {
        public string Name => "query-1";

        public IQueryable<TestRecord> Apply(
            IQueryable<TestRecord> query,
            IReadOnlyDictionary<string, object?>? parameters)
        {
            return query;
        }
    }

    private sealed class SecondNamedQuery :
        IQueryableRecordNamedQuery<TestRecord>
    {
        public string Name => "query-2";

        public IQueryable<TestRecord> Apply(
            IQueryable<TestRecord> query,
            IReadOnlyDictionary<string, object?>? parameters)
        {
            return query;
        }
    }
}