using Kaleido.Queryable.Metadata;

namespace Kaleido.UnitTests;

public sealed partial class QueryableCatalogTests
{
    private readonly QueryableTestFixture _fixture;
    private readonly QueryableCatalog _sut;

    public QueryableCatalogTests()
    {
        _fixture = new QueryableTestFixture();
        _sut = new QueryableCatalog(_fixture.Registry.Object,
            _fixture.Dispatcher.Object,
            _fixture.Descriptors.Object);
    }

    [Fact]
    public async Task QueryAsync_Generic_Should_Return_Dispatcher_Response()
    {
        var expected =
            new KaleidoQueryResponse<object>(
                TestData.Descriptor,
                0,
                []);

        _fixture.Dispatcher
            .Setup(x =>
                x.DispatchAsync<object>(
                    "functional-records",
                    TestData.Request,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result =
            await _sut.QueryAsync<object>(
                "functional-records",
                TestData.Request);

        Assert.Same(
            expected,
            result);
    }

    [Fact]
    public void Get_Should_Return_Descriptor()
    {
        _fixture.Registry
            .Setup(x =>
                x.Find("functional-records"))
            .Returns(TestData.Registration);

        _fixture.Descriptors
            .Setup(x =>
                x.Create(TestData.Metadata))
            .Returns(TestData.Descriptor);

        var result =
            _sut.Get("functional-records");

        Assert.Equal(
            TestData.Descriptor,
            result);
    }

    [Fact]
    public void Get_Should_Return_Null_When_Not_Found()
    {
        _fixture.Registry
            .Setup(x =>
                x.Find("missing"))
            .Returns((RecordRegistration?)null);

        var result =
            _sut.Get("missing");

        Assert.Null(result);
    }

    [Fact]
    public void GetAll_Should_Return_All_Descriptors()
    {
        _fixture.Registry
            .Setup(x => x.Registrations)
            .Returns(
            [
                TestData.Registration
            ]);

        _fixture.Descriptors
            .Setup(x =>
                x.Create(It.IsAny<RuntimeRecordMetadata>()))
            .Returns(TestData.Descriptor);

        var result =
            _sut.GetAll();

        Assert.Single(result);
    }

}
