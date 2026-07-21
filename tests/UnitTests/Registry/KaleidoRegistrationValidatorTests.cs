using Kaleido.Metadata;
using Kaleido.Queryable;
using Kaleido.Registry;
using Xunit;

namespace Kaleido.UnitTests.Registry;

public sealed class KaleidoRegistrationValidatorTests
{
    [Fact]
    public void Validate_Should_Succeed_When_Configuration_Is_Valid()
    {
        var discovery = TestData.CreateDiscovery();

        KaleidoRegistrationValidator.Validate(discovery);
    }

    [Fact]
    public void Validate_Should_Throw_When_Record_Name_Is_Duplicated()
    {
        var discovery = TestData.CreateDiscovery(
            records:
            [
                TestData.RecordDiscovery<TestRecord>("duplicate"),
                TestData.RecordDiscovery<OtherRecord>("duplicate")
            ],
            sources:
            [
                TestData.SourceDiscovery<TestRecord, TestSource>(),
                TestData.SourceDiscovery<OtherRecord, OtherSource>()
            ]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => KaleidoRegistrationValidator.Validate(discovery));

        Assert.Contains(
            "Duplicate record names",
            exception.Message);

        Assert.Contains(
            "duplicate",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Treat_Record_Names_As_Case_Insensitive()
    {
        var discovery = TestData.CreateDiscovery(
            records:
            [
                TestData.RecordDiscovery<TestRecord>("customer"),
                TestData.RecordDiscovery<OtherRecord>("CUSTOMER")
            ],
            sources:
            [
                TestData.SourceDiscovery<TestRecord, TestSource>(),
                TestData.SourceDiscovery<OtherRecord, OtherSource>()
            ]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => KaleidoRegistrationValidator.Validate(discovery));

        Assert.Contains(
            "Duplicate record names",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Record_Has_Multiple_Sources()
    {
        var discovery = TestData.CreateDiscovery(
            sources:
            [
                TestData.SourceDiscovery<TestRecord, TestSource>(),
                TestData.SourceDiscovery<TestRecord, SecondSource>()
            ]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => KaleidoRegistrationValidator.Validate(discovery));

        Assert.Contains(
            "Multiple sources registered",
            exception.Message);

        Assert.Contains(
            nameof(TestRecord),
            exception.Message);

        Assert.Contains(
            nameof(TestSource),
            exception.Message);

        Assert.Contains(
            nameof(SecondSource),
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Record_Has_No_Source()
    {
        var discovery = TestData.CreateDiscovery(
            records:
            [
                TestData.RecordDiscovery<TestRecord>()
            ],
            sources: []);

        var exception = Assert.Throws<InvalidOperationException>(
            () => KaleidoRegistrationValidator.Validate(discovery));

        Assert.Contains(
            nameof(TestRecord),
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Allow_Multiple_Named_Queries()
    {
        var discovery = TestData.CreateDiscovery(
            namedQueries:
            [
                TestData.NamedQueryDiscovery<TestRecord, QueryOne>(),
                TestData.NamedQueryDiscovery<TestRecord, QueryTwo>()
            ]);

        KaleidoRegistrationValidator.Validate(discovery);
    }

    [Fact]
    public void Validate_Should_Report_All_Missing_Record_Sources()
    {
        var discovery = TestData.CreateDiscovery(
            records:
            [
                TestData.RecordDiscovery<TestRecord>(),
                TestData.RecordDiscovery<OtherRecord>()
            ],
            sources: []);

        var exception = Assert.Throws<InvalidOperationException>(
            () => KaleidoRegistrationValidator.Validate(discovery));

        Assert.Contains(
            nameof(TestRecord),
            exception.Message);

        Assert.Contains(
            nameof(OtherRecord),
            exception.Message);
    }

    private static class TestData
    {
        public static KaleidoDiscoveryResult CreateDiscovery(
            IReadOnlyList<RecordDiscovery>? records = null,
            IReadOnlyList<SourceDiscovery>? sources = null,
            IReadOnlyList<NamedQueryDiscovery>? namedQueries = null)
        {
            return new KaleidoDiscoveryResult
            {
                Records = records ??
                [
                    RecordDiscovery<TestRecord>()
                ],

                Sources = sources ??
                [
                    SourceDiscovery<TestRecord, TestSource>()
                ],

                NamedQueries = namedQueries ??
                [
                    NamedQueryDiscovery<TestRecord, QueryOne>()
                ]
            };
        }

        public static RecordDiscovery RecordDiscovery<TRecord>(
            string? name = null)
        {
            return new RecordDiscovery(
                typeof(TRecord),
                new RuntimeRecordMetadata(
                    name ?? typeof(TRecord).Name,
                    "1.0.0",
                    "Unit Test",
                    [],
                    [],
                    null));
        }

        public static SourceDiscovery SourceDiscovery<TRecord, TSource>() where TRecord : class where TSource : class
        {
            return new SourceDiscovery(
                typeof(TRecord),
                typeof(IQueryableRecordSource<TRecord>),
                typeof(TSource));
        }

        public static NamedQueryDiscovery NamedQueryDiscovery<TRecord, TQuery>() where TRecord : class where TQuery : class
        {
            return new NamedQueryDiscovery(
                typeof(TRecord),
                typeof(IQueryableRecordNamedQuery<TRecord>),
                typeof(TQuery));
        }
    }

    private sealed class TestRecord
    {
    }

    private sealed class OtherRecord
    {
    }

    private sealed class TestSource
    {
    }

    private sealed class SecondSource
    {
    }

    private sealed class OtherSource
    {
    }

    private sealed class QueryOne
    {
    }

    private sealed class QueryTwo
    {
    }
}