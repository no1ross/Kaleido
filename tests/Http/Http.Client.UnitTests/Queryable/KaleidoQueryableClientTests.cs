using Kaleido.Platform.Queryable;

namespace Kaleido.Http.Client.Queryable.UnitTests;

public sealed class KaleidoQueryableClientTests : Kaleido.UnitTests.SutFixture
{
    private static readonly RegistryTransportRoute HttpRoute = new() { Transport = "http", GatewayRoute = "/member/1" };
    private static readonly QueryResult<FakeView> Answer = new(1, 0, 1, [new FakeView { Id = 42 }]);
    private readonly Mock<IKaleidoRegistryTargetResolver> targets = new();
    private readonly Mock<IKaleidoClientTransport> transport = new();
    private readonly List<KaleidoClientRequest> sent = [];

    private KaleidoQueryableClient CreateSut(string? version = null)
    {
        var source = new QueryableSourceResponse
        {
            ServiceName = "member",
            ServiceVersion = "1",
            Name = "My-Context",
            QueryUrl = "/member/1/queryable/my-context/query",
            Views = [new QueryableViewResponse { Name = "Grid", QueryUrl = "/member/1/queryable/my-context/published-view/query" }],
        };
        targets.Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KaleidoRegistryTarget(new KaleidoRegistry { Queryables = [source] },
                new RegistryServiceAvailability { ServiceName = "member", ServiceVersion = "1", IsCurrent = true, Transports = [HttpRoute] }, HttpRoute));
        transport.Setup(x => x.SendAsync<QueryResult<FakeView>>(Captured(), It.IsAny<CancellationToken>())).ReturnsAsync(Answer);
        return new KaleidoQueryableClient("member", version, targets.Object, transport.Object);
    }

    private KaleidoClientRequest Captured() => Capture.In(sent);
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;
    private sealed class FakeView { public int Id { get; init; } }
    private sealed class FakeParams { public string ProcessId { get; init; } = ""; }

    [Fact]
    public async Task QueryViewAsync_UsesThePublishedViewUrl()
    {
        var body = new QueryApiRequest<FakeParams>(new FakeParams(), new QueryApiBody());

        var result = await CreateSut().QueryViewAsync<FakeParams, FakeView>("My-Context", "Grid", body, Cancel);

        Assert.Same(Answer, result);
        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/member/1/queryable/my-context/published-view/query", request.Url);
        Assert.Same(body, request.Body);
        Assert.False(request.Idempotent);
    }

    [Fact]
    public async Task QuerySourceAsync_UsesThePublishedSourceUrl()
    {
        var body = new QueryApiRequest(new QueryApiBody());

        var result = await CreateSut().QuerySourceAsync<FakeView>("My-Context", body, Cancel);

        Assert.Same(Answer, result);
        var request = Assert.Single(sent);
        Assert.Equal("/member/1/queryable/my-context/query", request.Url);
        Assert.Equal("member", request.ServiceName);
        Assert.Same(body, request.Body);
    }

    [Fact]
    public async Task QuerySourceAsync_WithParameters_PostsTheParameters()
    {
        var body = new QueryApiRequest<FakeParams>(new FakeParams { ProcessId = "p-1" }, new QueryApiBody());

        await CreateSut().QuerySourceAsync<FakeParams, FakeView>("my-context", body, Cancel);

        Assert.Same(body, Assert.Single(sent).Body);
    }

    [Fact]
    public async Task QueryViewAsync_AnUnpublishedView_IsRefusedBeforeSending()
    {
        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut()
            .QueryViewAsync<FakeParams, FakeView>("My-Context", "other", new QueryApiRequest<FakeParams>(new FakeParams(), new QueryApiBody()), Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task QuerySourceAsync_WithoutASourceName_IsRefused(string? source)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateSut().QuerySourceAsync<FakeView>(source!, new QueryApiRequest(new QueryApiBody()), Cancel));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task QuerySourceAsync_AFailure_IsPassedOn()
    {
        var failure = new KaleidoHttpClientException(HttpClientErrorCodes.RequestFailed, "forbidden", HttpStatusCode.Forbidden);
        var sut = CreateSut();
        transport.Setup(x => x.SendAsync<QueryResult<FakeView>>(It.IsAny<KaleidoClientRequest>(), Cancel)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.QuerySourceAsync<FakeView>("My-Context", new QueryApiRequest(new QueryApiBody()), Cancel)));
    }
}
