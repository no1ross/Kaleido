using Kaleido.Metadata;
using Kaleido.Registry;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Kaleido.UnitTests;

public sealed class RecordDispatcherTests
{
    private readonly Fixture _fixture;
    private readonly RecordDispatcher _sut;

    public RecordDispatcherTests()
    {
        _fixture = new Fixture();

        _sut = new RecordDispatcher(
            _fixture.ScopeFactory.Object,
            _fixture.Registry.Object,
            _fixture.Descriptors.Object);
    }

    [Fact]
    public async Task DispatchAsync_Should_Find_Registration_By_Record_Key()
    {
        _fixture.SetupUntypedSuccess();

        await _sut.DispatchAsync(
            TestData.RecordKey,
            TestData.Request);

        _fixture.Registry.Verify(
            x => x.Find(TestData.RecordKey),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Create_Service_Scope()
    {
        _fixture.SetupUntypedSuccess();

        await _sut.DispatchAsync(
            TestData.RecordKey,
            TestData.Request);

        _fixture.ScopeFactory.Verify(
            x => x.CreateScope(),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Resolve_Engine_From_Scope()
    {
        _fixture.SetupUntypedSuccess();

        var engineType =
            typeof(IRecordQueryEngine<>)
                .MakeGenericType(typeof(TestRecord));

        await _sut.DispatchAsync(
            TestData.RecordKey,
            TestData.Request);

        _fixture.ServiceProvider.Verify(
            x => x.GetService(engineType),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Execute_Engine()
    {
        _fixture.SetupUntypedSuccess();

        await _sut.DispatchAsync(
            TestData.RecordKey,
            TestData.Request);

        _fixture.UntypedEngine.Verify(
            x => x.ExecuteAsync(
                TestData.Request,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Create_Descriptor()
    {
        _fixture.SetupUntypedSuccess();

        await _sut.DispatchAsync(
            TestData.RecordKey,
            TestData.Request);

        _fixture.Descriptors.Verify(
            x => x.Create(TestData.Metadata),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Return_Response()
    {
        _fixture.SetupUntypedSuccess();

        var result =
            await _sut.DispatchAsync(
                TestData.RecordKey,
                TestData.Request);

        Assert.Equal(
            TestData.TotalCount,
            result.TotalCount);

        Assert.Equal(
            TestData.TypedItems.Count,
            result.Items.Count);

        Assert.All(
            result.Items,
            x => Assert.IsType<TestRecord>(x));

        Assert.Same(
            TestData.Descriptor,
            result.Descriptor);
    }

    [Fact]
    public async Task DispatchAsync_Should_Dispose_Scope()
    {
        _fixture.SetupUntypedSuccess();

        await _sut.DispatchAsync(
            TestData.RecordKey,
            TestData.Request);

        _fixture.Scope.Verify(
            x => x.Dispose(),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Throw_When_Record_Is_Not_Registered()
    {
        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns((RecordRegistration?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.DispatchAsync(
                TestData.RecordKey,
                TestData.Request));

        _fixture.ScopeFactory.Verify(
            x => x.CreateScope(),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_Generic_Should_Return_Typed_Response()
    {
        _fixture.SetupTypedSuccess();

        var result =
            await _sut.DispatchAsync<TestRecord>(
                TestData.RecordKey,
                TestData.Request);

        Assert.Equal(
            TestData.TypedItems.Count,
            result.TotalCount);

        Assert.Same(
            TestData.TypedItems,
            result.Items);

        Assert.Same(
            TestData.Descriptor,
            result.Descriptor);
    }

    [Fact]
    public async Task DispatchAsync_Generic_Should_Resolve_Typed_Engine()
    {
        _fixture.SetupTypedSuccess();

        await _sut.DispatchAsync<TestRecord>(
            TestData.RecordKey,
            TestData.Request);

        _fixture.ServiceProvider.Verify(
            x => x.GetService(
                typeof(IRecordQueryEngine<TestRecord>)),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Generic_Should_Execute_Typed_Engine()
    {
        _fixture.SetupTypedSuccess();

        await _sut.DispatchAsync<TestRecord>(
            TestData.RecordKey,
            TestData.Request);

        _fixture.TypedEngine.Verify(
            x => x.ExecuteTypedAsync(
                TestData.Request,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Generic_Should_Throw_When_Record_Key_Maps_To_Different_Type()
    {
        var registration = new RecordRegistration(
            TestData.RecordKey,
            typeof(AnotherTestRecord),
            TestData.Metadata);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(registration);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DispatchAsync<TestRecord>(
                TestData.RecordKey,
                TestData.Request));

        _fixture.ScopeFactory.Verify(
            x => x.CreateScope(),
            Times.Never);
    }

    private sealed class Fixture
    {
        public Mock<IServiceScopeFactory> ScopeFactory { get; } = new();

        public Mock<IServiceScope> Scope { get; } = new();

        public Mock<IServiceProvider> ServiceProvider { get; } = new();

        public Mock<IRecordRegistry> Registry { get; } = new();

        public Mock<IRecordDescriptorFactory> Descriptors { get; } = new();

        public Mock<IRecordQueryEngine> UntypedEngine { get; } = new();

        public Mock<IRecordQueryEngine<TestRecord>> TypedEngine { get; } = new();

        public Fixture()
        {
            Scope
                .SetupGet(x => x.ServiceProvider)
                .Returns(ServiceProvider.Object);

            ScopeFactory
                .Setup(x => x.CreateScope())
                .Returns(Scope.Object);
        }

        public void SetupUntypedSuccess()
        {
            Registry
                .Setup(x => x.Find(TestData.RecordKey))
                .Returns(TestData.Registration);

            Descriptors
                .Setup(x => x.Create(TestData.Metadata))
                .Returns(TestData.Descriptor);

            ServiceProvider
                .Setup(x => x.GetService(
                    typeof(IRecordQueryEngine<>)
                        .MakeGenericType(typeof(TestRecord))))
                .Returns(UntypedEngine.Object);

            UntypedEngine
                .Setup(x => x.ExecuteAsync(
                    TestData.Request,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.QueryResult);
        }

        public void SetupTypedSuccess()
        {
            Registry
                .Setup(x => x.Find(TestData.RecordKey))
                .Returns(TestData.Registration);

            Descriptors
                .Setup(x => x.Create(TestData.Metadata))
                .Returns(TestData.Descriptor);

            ServiceProvider
                .Setup(x => x.GetService(
                    typeof(IRecordQueryEngine<TestRecord>)))
                .Returns(TypedEngine.Object);

            TypedEngine
                .Setup(x => x.ExecuteTypedAsync(
                    TestData.Request,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.TypedQueryResult);
        }
    }

    private static class TestData
    {
        public const string RecordKey = "test";

        public static readonly KaleidoQueryRequest Request =
            new(null, null, null);

        public const int TotalCount = 2;

        public static readonly RuntimeRecordMetadata Metadata =
            new(
                "test",
                "1.0.0",
                "Unit Test",
                [],
                [],
                null);

        public static readonly RecordDescriptor Descriptor =
            new(
                "test",
                "1.0.0",
                "Unit Test",
                [],
                [],
                null);

        public static readonly List<TestRecord> TypedItems =
        [
            new(),
            new()
        ];

        public static readonly IRecordQueryResult QueryResult =
            new QueryResult<TestRecord>(
                TypedItems,
                TotalCount,
                Metadata);

        public static readonly QueryResult<TestRecord> TypedQueryResult =
            new(
                TypedItems,
                TotalCount,
                Metadata);

        public static readonly RecordRegistration Registration =
            new(
                RecordKey,
                typeof(TestRecord),
                Metadata);
    }
}