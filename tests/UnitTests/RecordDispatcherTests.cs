using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Kaleido.Metadata;
using Kaleido;
using Kaleido.Registry;
using Moq;

namespace Kaleido.UnitTests;

public sealed class RecordDispatcherTests
{
    private readonly Fixture _fixture;
    private readonly RecordDispatcher _sut;

    public RecordDispatcherTests()
    {
        _fixture = new Fixture();
        _sut = _fixture.CreateSut();
    }

    [Fact]
    public async Task DispatchAsync_Should_Find_Registration_By_Record_Key()
    {
        var request = TestData.Request;
        var result = TestData.QueryResult();

        var engine = new Mock<IRecordQueryEngine>();
        engine
            .Setup(x => x.ExecuteAsync(
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(TestData.Registration);

        _fixture.Descriptors
            .Setup(x => x.Create(TestData.Metadata))
            .Returns(TestData.Descriptor);

        _fixture.ConfigureScopeService(
            typeof(IRecordQueryEngine<>).MakeGenericType(typeof(TestRecord)),
            engine.Object);

        await _sut.DispatchAsync(
            TestData.RecordKey,
            request);

        _fixture.Registry.Verify(
            x => x.Find(TestData.RecordKey),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Create_Service_Scope()
    {
        var request = TestData.Request;
        var result = TestData.QueryResult();

        var engine = new Mock<IRecordQueryEngine>();
        engine
            .Setup(x => x.ExecuteAsync(
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(TestData.Registration);

        _fixture.Descriptors
            .Setup(x => x.Create(TestData.Metadata))
            .Returns(TestData.Descriptor);

        _fixture.ConfigureScopeService(
            typeof(IRecordQueryEngine<>).MakeGenericType(typeof(TestRecord)),
            engine.Object);

        await _sut.DispatchAsync(
            TestData.RecordKey,
            request);

        _fixture.ScopeFactory.Verify(
            x => x.CreateScope(),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Resolve_Engine_From_Scope()
    {
        var request = TestData.Request;
        var result = TestData.QueryResult();

        var engineType =
            typeof(IRecordQueryEngine<>)
                .MakeGenericType(typeof(TestRecord));

        var engine = new Mock<IRecordQueryEngine>();
        engine
            .Setup(x => x.ExecuteAsync(
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(TestData.Registration);

        _fixture.Descriptors
            .Setup(x => x.Create(TestData.Metadata))
            .Returns(TestData.Descriptor);

        _fixture.ConfigureScopeService(
            engineType,
            engine.Object);

        await _sut.DispatchAsync(
            TestData.RecordKey,
            request);

        _fixture.ServiceProvider.Verify(
            x => x.GetService(engineType),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Execute_Engine_With_Request_And_CancellationToken()
    {
        var request = TestData.Request;
        var cancellationToken = new CancellationTokenSource().Token;
        var result = TestData.QueryResult();

        var engine = new Mock<IRecordQueryEngine>();
        engine
            .Setup(x => x.ExecuteAsync(
                request,
                cancellationToken))
            .ReturnsAsync(result);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(TestData.Registration);

        _fixture.Descriptors
            .Setup(x => x.Create(TestData.Metadata))
            .Returns(TestData.Descriptor);

        _fixture.ConfigureScopeService(
            typeof(IRecordQueryEngine<>).MakeGenericType(typeof(TestRecord)),
            engine.Object);

        await _sut.DispatchAsync(
            TestData.RecordKey,
            request,
            cancellationToken);

        engine.Verify(
            x => x.ExecuteAsync(
                request,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Create_Descriptor_From_Result_Metadata()
    {
        var request = TestData.Request;
        var result = TestData.QueryResult();

        var engine = new Mock<IRecordQueryEngine>();
        engine
            .Setup(x => x.ExecuteAsync(
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(TestData.Registration);

        _fixture.Descriptors
            .Setup(x => x.Create(TestData.Metadata))
            .Returns(TestData.Descriptor);

        _fixture.ConfigureScopeService(
            typeof(IRecordQueryEngine<>).MakeGenericType(typeof(TestRecord)),
            engine.Object);

        await _sut.DispatchAsync(
            TestData.RecordKey,
            request);

        _fixture.Descriptors.Verify(
            x => x.Create(TestData.Metadata),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Return_Response_From_Engine_Result()
    {
        var request = TestData.Request;
        var result = TestData.QueryResult();

        var engine = new Mock<IRecordQueryEngine>();
        engine
            .Setup(x => x.ExecuteAsync(
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(TestData.Registration);

        _fixture.Descriptors
            .Setup(x => x.Create(TestData.Metadata))
            .Returns(TestData.Descriptor);

        _fixture.ConfigureScopeService(
            typeof(IRecordQueryEngine<>).MakeGenericType(typeof(TestRecord)),
            engine.Object);

        var response = await _sut.DispatchAsync(
            TestData.RecordKey,
            request);

        Assert.Same(TestData.Descriptor, response.Descriptor);
        Assert.Equal(TestData.TotalCount, response.TotalCount);
        Assert.Equal(TestData.Items, response.Items);
    }

    [Fact]
    public async Task DispatchAsync_Should_Dispose_Scope()
    {
        var request = TestData.Request;
        var result = TestData.QueryResult();

        var engine = new Mock<IRecordQueryEngine>();
        engine
            .Setup(x => x.ExecuteAsync(
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(TestData.Registration);

        _fixture.Descriptors
            .Setup(x => x.Create(TestData.Metadata))
            .Returns(TestData.Descriptor);

        _fixture.ConfigureScopeService(
            typeof(IRecordQueryEngine<>).MakeGenericType(typeof(TestRecord)),
            engine.Object);

        await _sut.DispatchAsync(
            TestData.RecordKey,
            request);

        _fixture.Scope.Verify(
            x => x.Dispose(),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Should_Throw_When_Record_Is_Not_Registered()
    {
        var request = TestData.Request;

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns((RecordRegistration?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.DispatchAsync(
                TestData.RecordKey,
                request));

        _fixture.ScopeFactory.Verify(
            x => x.CreateScope(),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_Generic_Should_Return_Typed_Response()
    {
        var request = TestData.Request;

        var typedResult = new QueryResult<TestRecord>(
            Items: TestData.TypedItems,
            TotalCount: TestData.TypedItems.Count,
            RuntimeMetadata: TestData.Metadata);

        var engine = new Mock<IRecordQueryEngine<TestRecord>>();
        engine
            .Setup(x => x.ExecuteTypedAsync(
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(typedResult);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(TestData.Registration);

        _fixture.Descriptors
            .Setup(x => x.Create(TestData.Metadata))
            .Returns(TestData.Descriptor);

        _fixture.ConfigureScopeService(
            typeof(IRecordQueryEngine<TestRecord>),
            engine.Object);

        var response =
            await _sut.DispatchAsync<TestRecord>(
                TestData.RecordKey,
                request);

        Assert.Same(TestData.Descriptor, response.Descriptor);
        Assert.Equal(TestData.TypedItems.Count, response.TotalCount);
        Assert.Same(TestData.TypedItems, response.Items);
    }

    [Fact]
    public async Task DispatchAsync_Generic_Should_Throw_When_Record_Key_Maps_To_Different_Type()
    {
        var request = TestData.Request;

        var registration = new RecordRegistration(
            TestData.RecordKey,
            typeof(OtherRecord),
            TestData.Metadata);

        _fixture.Registry
            .Setup(x => x.Find(TestData.RecordKey))
            .Returns(registration);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DispatchAsync<TestRecord>(
                TestData.RecordKey,
                request));

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

        public Fixture()
        {
            Scope
                .SetupGet(x => x.ServiceProvider)
                .Returns(ServiceProvider.Object);

            ScopeFactory
                .Setup(x => x.CreateScope())
                .Returns(Scope.Object);
        }

        public RecordDispatcher CreateSut()
        {
            return new RecordDispatcher(
                ScopeFactory.Object,
                Registry.Object,
                Descriptors.Object);
        }

        public void ConfigureScopeService(
            Type serviceType,
            object service)
        {
            ServiceProvider
                .Setup(x => x.GetService(serviceType))
                .Returns(service);
        }
    }

}
