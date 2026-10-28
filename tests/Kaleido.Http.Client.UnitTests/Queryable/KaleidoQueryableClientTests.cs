using Kaleido.Http.Queryable;

namespace Kaleido.Http.Client.Queryable.UnitTests;

// What the queryable client sends (#498): each query's URL, body and flags, handed to the transport,
// which is tested in KaleidoClientTransportTests.
public sealed class KaleidoQueryableClientTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IKaleidoClientTransport> transport = new();
    private readonly Mock<IKaleidoRemoteRegistry> remoteRegistry = new();
    private readonly ICorrelationHeaderStamper stamper = Mock.Of<ICorrelationHeaderStamper>();
    private readonly List<KaleidoClientRequest> sent = [];

    private static readonly QueryResult<FakeView> Answer = new(1, 0, 1, [new FakeView { Id = 42 }]);

    private KaleidoQueryableClient CreateSut(string routePrefix = "member")
    {
        var catalog = new Mock<IKaleidoClientCatalog>();
        catalog.Setup(x => x.Find("MemberClient")).Returns(Definition(routePrefix));

        transport.Setup(x => x.SendAsync<QueryResult<FakeView>>(Captured(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Answer);

        return new("MemberClient", catalog.Object, transport.Object, remoteRegistry.Object, stamper);
    }

    private KaleidoClientRequest Captured() =>
        Capture.In(sent);

    private static KaleidoClientDefinition Definition(string routePrefix) =>
        new("MemberClient", routePrefix, RegistryTtl: null, StrictRegistryProbe: false, HealthCheckTimeout: TimeSpan.FromSeconds(5));

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private sealed class FakeView
    {
        public int Id { get; init; }
    }

    private sealed class FakeParams
    {
        public string ProcessId { get; init; } = "";
    }

    // ---------------------------------------------------------------------------
    // Registry
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_ReturnsTheQueryablesOfTheClientsRemoteRegistry()
    {
        var sut = CreateSut();
        remoteRegistry.Setup(x => x.GetAsync(Definition("member"), stamper, Cancel))
            .ReturnsAsync(new AggregatedRegistryResponse { Queryables = [new QueryableSourceResponse { Name = "my-context", ServiceName = "member" }] });

        var result = await sut.GetRegistryAsync(Cancel);

        Assert.Equal("my-context", Assert.Single(result).Name);
    }

    [Fact]
    public async Task InvalidateRegistryAsync_DropsTheClientsRemoteRegistry()
    {
        await CreateSut().InvalidateRegistryAsync(Cancel);

        remoteRegistry.Verify(x => x.InvalidateAsync("MemberClient", Cancel), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Queries
    // ---------------------------------------------------------------------------

    // A query is not idempotent-keyed; a 404 (an unknown source or view) drops the discovery snapshot.
    [Fact]
    public async Task QueryViewAsync_PostsTheRequestToTheViewQueryUrl()
    {
        var body = new QueryApiRequest<FakeParams>(new FakeParams(), new QueryApiBody());
        var sut = CreateSut();

        var result = await sut.QueryViewAsync<FakeParams, FakeView>("My-Context", "Grid", body, Cancel);

        Assert.Same(Answer, result);
        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/member/queryable/my-context/grid/query", request.Url);
        Assert.Same(body, request.Body);
        Assert.False(request.Idempotent);
        Assert.True(request.InvalidatesRegistryOnNotFound);
        Assert.Equal("Query of view 'Grid' on source 'My-Context' in 'member'", request.Description);
    }

    [Fact]
    public async Task QuerySourceAsync_PostsTheRequestToTheSourceQueryUrl()
    {
        var body = new QueryApiRequest(new QueryApiBody());
        var sut = CreateSut();

        var result = await sut.QuerySourceAsync<FakeView>("My-Context", body, Cancel);

        Assert.Same(Answer, result);
        var request = Assert.Single(sent);
        Assert.Equal("/member/queryable/my-context/query", request.Url);
        Assert.Same(body, request.Body);
        Assert.True(request.InvalidatesRegistryOnNotFound);
        Assert.Equal("Query of source 'My-Context' in 'member'", request.Description);
    }

    [Fact]
    public async Task QuerySourceAsync_WithParameters_PostsTheParameters()
    {
        var body = new QueryApiRequest<FakeParams>(new FakeParams { ProcessId = "p-1" }, new QueryApiBody());
        var sut = CreateSut();

        await sut.QuerySourceAsync<FakeParams, FakeView>("my-context", body, Cancel);

        var request = Assert.Single(sent);
        Assert.Equal("/member/queryable/my-context/query", request.Url);
        Assert.Same(body, request.Body);
    }

    // Query URLs come from the route prefix, never from the registry (#381).
    [Fact]
    public async Task QuerySourceAsync_WithoutARoutePrefix_UsesTheDefaultUrl_AndReadsNoRegistry()
    {
        await CreateSut(routePrefix: "").QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(new QueryApiBody()), Cancel);

        Assert.Equal("/queryable/my-context/query", Assert.Single(sent).Url);
        remoteRegistry.Verify(x => x.GetAsync(It.IsAny<KaleidoClientDefinition>(), It.IsAny<ICorrelationHeaderStamper>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueryViewAsync_KeepsEachNameOneRouteSegment()
    {
        await CreateSut().QueryViewAsync<FakeParams, FakeView>("../registry", "a/b", new QueryApiRequest<FakeParams>(new FakeParams(), new QueryApiBody()), Cancel);

        Assert.Equal("/member/queryable/..%2Fregistry/a%2Fb/query", Assert.Single(sent).Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task QuerySourceAsync_WithoutASourceName_IsRefused(string? source)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateSut().QuerySourceAsync<FakeView>(source!, new QueryApiRequest(new QueryApiBody()), Cancel));

        Assert.Empty(sent);
    }

    // A failure is the transport's answer, passed on as it is.
    [Fact]
    public async Task QuerySourceAsync_AFailure_IsPassedOn()
    {
        var failure = new KaleidoHttpClientException(HttpClientErrorCodes.RequestFailed, "forbidden", HttpStatusCode.Forbidden);
        var sut = CreateSut();
        transport.Setup(x => x.SendAsync<QueryResult<FakeView>>(It.IsAny<KaleidoClientRequest>(), Cancel)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(new QueryApiBody()), Cancel)));
    }
}
