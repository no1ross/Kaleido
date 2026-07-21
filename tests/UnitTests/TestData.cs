using Kaleido.Metadata;
using Moq;

namespace Kaleido.UnitTests;

public static class TestData
{
    public const string RecordKey = "test-record";

    public const int TotalCount = 2;

    public static readonly RuntimeRecordMetadata Metadata =
        new(
            Name: RecordKey,
            Version: "1.0.0",
            Source: "Unit Test",
            Fields: [],
            AllowedQueries: [],
            Pageable: null);

    public static readonly RecordRegistration Registration =
        new(
            RecordKey,
            typeof(TestRecord),
            Metadata);

    public static readonly RecordDescriptor Descriptor =
        new(
            Name: RecordKey,
            Version: "1.0.0",
            Source: "Unit Test",
            Fields: [],
            AllowedQueries: [],
            Pageable: null);

    public static KaleidoQueryRequest Request =>
        new(
            QueryName: null,
            Query: null,
            Parameters: null);

    public static KaleidoQueryResponse Response =>
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

    public static IRecordQueryResult QueryResult()
    {
        var result = new Mock<IRecordQueryResult>();

        result
            .SetupGet(x => x.RuntimeMetadata)
            .Returns(Metadata);

        result
            .SetupGet(x => x.TotalCount)
            .Returns(TotalCount);

        result
            .SetupGet(x => x.ItemsAsObjects)
            .Returns(Items);

        return result.Object;
    }
}

public sealed class TestRecord
{
}

public sealed class OtherRecord
{
}
