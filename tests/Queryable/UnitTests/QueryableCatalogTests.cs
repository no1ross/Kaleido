using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Moq;
using Xunit;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableCatalogTests
{
    [Fact]
    public void GetRecordDescriptors_ShouldReturnMetadataFromRegistry()
    {
        // Arrange
        var registrations =
            new[]
            {
                CreateRegistration<TestRecord>(
                    "record-one"),

                CreateRegistration<AnotherRecord>(
                    "record-two")
            };

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.Registrations)
            .Returns(registrations);

        var dispatcher =
            new Mock<IRecordDispatcher>();

        var sut =
            new QueryableCatalog(
                registry.Object,
                dispatcher.Object);

        // Act
        var result =
            sut.GetRecordDescriptors();

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Contains(
            result,
            x => x.Name == "record-one");

        Assert.Contains(
            result,
            x => x.Name == "record-two");
    }

    [Fact]
    public async Task QueryAsync_ShouldDispatchRequest()
    {
        // Arrange
        var registry =
            new Mock<IRecordRegistry>();

        var dispatcher =
            new Mock<IRecordDispatcher>();

        var request =
            new QueryRequest();

        var response =
            new QueryResult<TestRecord>(
                1,
                0,
                25,
                []);

        dispatcher
            .Setup(x => x.DispatchAsync<TestRecord>(
                "test-record",
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut =
            new QueryableCatalog(
                registry.Object,
                dispatcher.Object);

        // Act
        var result =
            await sut.QueryAsync<TestRecord>(
                "test-record",
                request);

        // Assert
        Assert.Same(
            response,
            result);

        dispatcher.Verify(
            x => x.DispatchAsync<TestRecord>(
                "test-record",
                request,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryAsync_ShouldPassCancellationToken()
    {
        // Arrange
        var registry =
            new Mock<IRecordRegistry>();

        var dispatcher =
            new Mock<IRecordDispatcher>();

        var request =
            new QueryRequest();

        var cancellationToken =
            new CancellationTokenSource().Token;

        var response =
            new QueryResult<TestRecord>(
                0,
                0,
                25,
                []);

        dispatcher
            .Setup(x => x.DispatchAsync<TestRecord>(
                "test-record",
                request,
                cancellationToken))
            .ReturnsAsync(response);

        var sut =
            new QueryableCatalog(
                registry.Object,
                dispatcher.Object);

        // Act
        await sut.QueryAsync<TestRecord>(
            "test-record",
            request,
            cancellationToken);

        // Assert
        dispatcher.Verify(
            x => x.DispatchAsync<TestRecord>(
                "test-record",
                request,
                cancellationToken),
            Times.Once);
    }

    private static RecordRegistration CreateRegistration<TRecord>(
        string name)
    {
        return new RecordRegistration(
            typeof(TRecord),
            typeof(TestSource),
            new RecordMetadata(
                name,
                "Test Record",
                "1.0.0",
                "Unit Test",
                [],
                null),
            []);
    }

    private sealed record TestRecord;

    private sealed record AnotherRecord;

    private sealed class TestSource
    {
    }
}