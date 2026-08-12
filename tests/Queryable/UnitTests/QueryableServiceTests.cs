using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableServiceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenScopeFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new QueryableService(
                null!,
                Mock.Of<IQueryContextRegistry>()));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenRegistryIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new QueryableService(
                Mock.Of<IServiceScopeFactory>(),
                null!));
    }

    [Fact]
    public async Task DispatchAsync_ShouldThrow_WhenRecordIsNotRegistered()
    {
        var registry =
            new Mock<IQueryContextRegistry>();

        registry
            .Setup(x => x.Find("test"))
            .Returns((QueryContextRegistration?)null);

        var dispatcher =
            new QueryableService(
                Mock.Of<IServiceScopeFactory>(),
                registry.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => dispatcher.QueryAsync<TestRecord>(
                "test",
                new QueryRequest()));
    }

    [Fact]
    public async Task DispatchAsync_ShouldThrow_WhenRecordTypeDoesNotMatch()
    {
        var registry =
            new Mock<IQueryContextRegistry>();

        registry
            .Setup(x => x.Find("test"))
            .Returns(CreateRegistration(typeof(AnotherRecord)));

        var dispatcher =
            new QueryableService(
                Mock.Of<IServiceScopeFactory>(),
                registry.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.QueryAsync<TestRecord>(
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
            new Mock<IQueryContextEngine<TestRecord>>();

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
                    typeof(IQueryContextEngine<TestRecord>)))
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
            new Mock<IQueryContextRegistry>();

        registry
            .Setup(x => x.Find("test"))
            .Returns(CreateRegistration(typeof(TestRecord)));

        var dispatcher =
            new QueryableService(
                scopeFactory.Object,
                registry.Object);

        await dispatcher.QueryAsync<TestRecord>(
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
            new Mock<IQueryContextEngine<TestRecord>>();

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
                    typeof(IQueryContextEngine<TestRecord>)))
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
            new Mock<IQueryContextRegistry>();

        registry
            .Setup(x => x.Find("test"))
            .Returns(CreateRegistration(typeof(TestRecord)));

        var dispatcher =
            new QueryableService(
                scopeFactory.Object,
                registry.Object);

        var response =
            await dispatcher.QueryAsync<TestRecord>(
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

    private static QueryContextRegistration CreateRegistration(
        Type recordType)
    {
        return new QueryRegistration(
            recordType,
            typeof(object),
            new QueryMetadata(
                "test",
                "Test",
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