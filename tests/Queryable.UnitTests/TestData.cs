using Kaleido.Queryable.Metadata;
using Moq;

namespace Kaleido.UnitTests;

public static class TestData
{
    public const string RecordKey = "test-record";

    public const int TotalCount = 2;

    public static readonly RuntimeRecordMetadata Metadata =
        new(
            Key: RecordKey,
            Name: RecordKey,
            Version: "1.0.0",
            Description: null,
            Source: "Unit Test",
            Fields: [],
            AllowedQueries: [],
            Pageable: null);

    public static readonly RecordRegistration Registration =
        new(
            typeof(TestRecord),
            Metadata);

    public static readonly RecordDescriptor Descriptor =
        new(
            Key: RecordKey,
            Name: RecordKey,
            Version: "1.0.0",
            Description: null,
            Source: "Unit Test",
            Fields: [],
            AllowedQueries: [],
            Pageable: null);

    public static KaleidoQueryRequest Request =>
        new(
            QueryName: null,
            Query: null,
            Parameters: null);

    public static KaleidoQueryResponse<TestRecord> Response =>
        new(
            Descriptor: null!,
            TotalCount: 0,
            Items: []);

    public static readonly IReadOnlyList<object> Items =
    [
        new object(),
            new object()
    ];

    public static readonly IReadOnlyList<TestRecord> TypedItems =
    [
        new(),
            new()
    ];
}

public sealed class InvalidRecord
{
}

[KaleidoRecord("TestRecord", null, "1.0.0", "Unit Test")]
public sealed record TestRecord
{
    [Filterable]
    public string Id { get; init; } = string.Empty;
}

[KaleidoRecord("AnotherTestRecord", "1.0.0", "Unit Test")]
public sealed record AnotherTestRecord
{
    [Filterable]
    public string Id { get; init; } = string.Empty;
}

public sealed class TestRecordSource : IQueryableRecordSource<TestRecord>
{
    public IQueryable<TestRecord> CreateQuery(RecordExecutionContext executionContext)
    {
        throw new NotImplementedException();
    }
}