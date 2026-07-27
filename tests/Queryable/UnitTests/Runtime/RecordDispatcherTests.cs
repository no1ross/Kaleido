using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Kaleido.Queryable.UnitTests.Runtime;

public sealed class RecordDispatcherTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenScopeFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RecordDispatcher(
                null!,
                Mock.Of<IRecordRegistry>()));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenRegistryIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RecordDispatcher(
                Mock.Of<IServiceScopeFactory>(),
                null!));
    }

    [Fact]
    public async Task DispatchAsync_ShouldThrow_WhenRecordIsNotRegistered()
    {
        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.Find("test"))
            .Returns((RecordRegistration?)null);

        var dispatcher =
            new RecordDispatcher(
                Mock.Of<IServiceScopeFactory>(),
                registry.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => dispatcher.DispatchAsync<TestRecord>(
                "test",
                new QueryRequest()));
    }

    [Fact]
    public async Task DispatchAsync_ShouldThrow_WhenRecordTypeDoesNotMatch()
    {
        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.Find("test"))
            .Returns(CreateRegistration(typeof(AnotherRecord)));

        var dispatcher =
            new RecordDispatcher(
                Mock.Of<IServiceScopeFactory>(),
                registry.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync<TestRecord>(
                "test",
                new QueryRequest()));
    }

    [Fact]
    public async Task DispatchAsync_ShouldExecuteQueryEngine()
    {
        var request =
            new QueryRequest();

        var result =
            new QueryResult<TestRecord>(
                1,
                0,
                1,
                [new TestRecord("A")]);

        var engine =
            new Mock<IRecordQueryEngine<TestRecord>>();

        engine
            .Setup(x =>
                x.ExecuteAsync(
                    request,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var provider =
            new Mock<IServiceProvider>();

        provider
            .Setup(x =>
                x.GetService(
                    typeof(IRecordQueryEngine<TestRecord>)))
            .Returns(engine.Object);

        var scope =
            new Mock<IServiceScope>();

        scope
            .SetupGet(x => x.ServiceProvider)
            .Returns(provider.Object);

        var scopeFactory =
            new Mock<IServiceScopeFactory>();

        scopeFactory
            .Setup(x => x.CreateScope())
            .Returns(scope.Object);

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.Find("test"))
            .Returns(CreateRegistration(typeof(TestRecord)));

        var dispatcher =
            new RecordDispatcher(
                scopeFactory.Object,
                registry.Object);

        await dispatcher.DispatchAsync<TestRecord>(
            "test",
            request);

        engine.Verify(
            x =>
                x.ExecuteAsync(
                    request,
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ShouldReturnQueryResult()
    {
        var item =
            new TestRecord("A");

        var result =
            new QueryResult<TestRecord>(
                1,
                0,
                25,
                [item]);

        var engine =
            new Mock<IRecordQueryEngine<TestRecord>>();

        engine
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<QueryRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var provider =
            new Mock<IServiceProvider>();

        provider
            .Setup(x =>
                x.GetService(
                    typeof(IRecordQueryEngine<TestRecord>)))
            .Returns(engine.Object);

        var scope =
            new Mock<IServiceScope>();

        scope
            .SetupGet(x => x.ServiceProvider)
            .Returns(provider.Object);

        var scopeFactory =
            new Mock<IServiceScopeFactory>();

        scopeFactory
            .Setup(x => x.CreateScope())
            .Returns(scope.Object);

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.Find("test"))
            .Returns(CreateRegistration(typeof(TestRecord)));

        var dispatcher =
            new RecordDispatcher(
                scopeFactory.Object,
                registry.Object);

        var response =
            await dispatcher.DispatchAsync<TestRecord>(
                "test",
                new QueryRequest());

        Assert.Equal(
            1,
            response.TotalCount);

        Assert.Equal(
            0,
            response.Offset);

        Assert.Equal(
            25,
            response.PageSize);

        Assert.Single(
            response.Records);

        Assert.Same(
            item,
            response.Records.Single());
    }

    private static RecordRegistration CreateRegistration(
        Type recordType)
    {
        return new RecordRegistration(
            recordType,
            typeof(object),
            new RecordMetadata(
                "test",
                "Test",
                "1.0",
                "Unit Test",
                [],
                null),
            []);
    }

    internal sealed record TestRecord(
        string Name);

    internal sealed record AnotherRecord(
        string Name);
}