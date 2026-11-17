using System.Security.Claims;
using System.Text.Json;

namespace Kaleido.Queryable.Http.Client.UnitTests;

public sealed class KaleidoQueryableClientTests : Kaleido.UnitTests.SutFixture, IDisposable
{
    private static readonly RegistryTransportRoute HttpRoute = new() { Transport = "http", GatewayRoute = "/member/1" };
    private readonly Mock<IKaleidoRegistryTargetResolver> targets = new();
    private readonly Mock<IHttpClientFactory> clients = new();
    private readonly Mock<IKaleidoInboundCallerAccessor> inboundCaller = new();
    private readonly Mock<IKaleidoCorrelationContextAccessor> correlation = new();
    private readonly Mock<HttpMessageHandler> handler = new(MockBehavior.Strict);
    private readonly List<Sent> sent = [];
    private readonly List<HttpClient> created = [];
    private HttpStatusCode responseStatus = HttpStatusCode.OK;

    private sealed record Sent(HttpMethod Method, string Path, string Body, KaleidoCallContext Call, string? RequestId);
    public sealed class FakeView { public int Id { get; init; } }
    private sealed class FakeParams { public string ProcessId { get; init; } = string.Empty; }

    public KaleidoQueryableClientTests()
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { RequestId = "query-request" });
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken token) => RespondAsync(request, token));
        clients.Setup(x => x.CreateClient(KaleidoQueryableClient.ClientName)).Returns(() =>
        {
            var client = new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("https://gateway.test/") };
            created.Add(client);
            return client;
        });
        Target(Source());
    }

    private static QueryableRegistryItem Source(RegistryTransportBindings? bindings = null) => new()
    {
        ServiceName = "member",
        ServiceVersion = "1",
        Name = "My-Context",
        Bindings = bindings ?? Query("/member/1/queryable/my-context/query"),
        Views = [new QueryableRegistryViewItem { Name = "Grid", Bindings = Query("/member/1/queryable/my-context/published-view/query") }],
    };

    private static RegistryTransportBindings Query(string path) => new()
    {
        Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = path }],
    };

    private void Target(QueryableRegistryItem source) =>
        targets.Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KaleidoRegistryTarget(new KaleidoRegistry { Queryables = [source] },
                new RegistryServiceAvailability { ServiceName = "member", ServiceVersion = "1", IsCurrent = true, Transports = [HttpRoute] }, HttpRoute));

    private KaleidoQueryableClient CreateSut(string? version = null) =>
        new("member", version, targets.Object, clients.Object, inboundCaller.Object, correlation.Object);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken token)
    {
        var body = await Assert.IsAssignableFrom<HttpContent>(request.Content).ReadAsStringAsync(token);
        sent.Add(new Sent(request.Method, request.RequestUri?.AbsolutePath ?? string.Empty, body,
            KaleidoCallContext.Of(request), request.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out var ids) ? Assert.Single(ids) : null));
        return responseStatus == HttpStatusCode.OK
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new QueryResult<FakeView>(1, 0, 1, [new FakeView { Id = 42 }]), options: KaleidoJsonOptions.Options),
            }
            : new HttpResponseMessage(responseStatus)
            {
                Content = JsonContent.Create(new KaleidoErrorResponse([new KaleidoError("qry_forbidden", "No access")]), options: KaleidoJsonOptions.Options),
            };
    }

    [Fact]
    public async Task QueryViewAsync_UsesThePublishedViewBindingAndNeutralParameters()
    {
        var body = new QueryRequest<FakeParams>(new FakeParams { ProcessId = "p-1" }, new QueryBody(SearchText: "scan"));

        var result = await CreateSut().QueryViewAsync<FakeParams, FakeView>("My-Context", "Grid", body, Cancel);

        Assert.Equal(42, Assert.Single(result.Results).Id);
        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/member/1/queryable/my-context/published-view/query", request.Path);
        using var json = JsonDocument.Parse(request.Body);
        Assert.Equal("p-1", json.RootElement.GetProperty("parameters").GetProperty("processId").GetString());
        Assert.Equal("scan", json.RootElement.GetProperty("query").GetProperty("searchText").GetString());
        Assert.Equal("query-request", request.RequestId);
    }

    [Fact]
    public async Task QuerySourceAsync_UsesThePublishedSourceBinding()
    {
        var result = await CreateSut("1").QuerySourceAsync<FakeView>("my-context", new QueryRequest(), Cancel);

        Assert.Equal(42, Assert.Single(result.Results).Id);
        Assert.Equal("/member/1/queryable/my-context/query", Assert.Single(sent).Path);
        targets.Verify(x => x.ResolveAsync("member", "1", It.Is<IReadOnlyCollection<string>>(transports => transports.Count == 1 && transports.Contains("http")), Cancel), Times.Once);
    }

    [Fact]
    public async Task QuerySourceAsync_WithParameters_PostsTheNeutralParameters()
    {
        await CreateSut().QuerySourceAsync<FakeParams, FakeView>("My-Context",
            new QueryRequest<FakeParams>(new FakeParams { ProcessId = "p-2" }), Cancel);

        using var json = JsonDocument.Parse(Assert.Single(sent).Body);
        Assert.Equal("p-2", json.RootElement.GetProperty("parameters").GetProperty("processId").GetString());
    }

    [Fact]
    public async Task QueryViewAsync_AnUnpublishedView_IsRefusedBeforeSending()
    {
        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut()
            .QueryViewAsync<FakeParams, FakeView>("My-Context", "other", new QueryRequest<FakeParams>(new FakeParams()), Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task QuerySourceAsync_WithoutExactlyOnePublishedPostQueryBinding_IsRefused(int count)
    {
        var operations = Enumerable.Range(0, count)
            .Select(_ => new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/member/1/queryable/my-context/query" })
            .ToArray();
        Target(Source(new RegistryTransportBindings { Http = operations }));

        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().QuerySourceAsync<FakeView>("My-Context", new QueryRequest(), Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData("/member/2/queryable/my-context/query")]
    [InlineData("https://other.test/member/1/queryable/my-context/query")]
    [InlineData("/member/1/../query")]
    public async Task QuerySourceAsync_AnUnsafePublishedBinding_IsRefusedBeforeSending(string path)
    {
        Target(Source(Query(path)));

        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().QuerySourceAsync<FakeView>("My-Context", new QueryRequest(), Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task QuerySourceAsync_BeforeRegistryHydration_DoesNotSend()
    {
        targets.Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), Cancel))
            .ThrowsAsync(new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable, "The Registry has not been read."));

        await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().QuerySourceAsync<FakeView>("My-Context", new QueryRequest(), Cancel));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task QuerySourceAsync_AnInboundCall_CarriesTheCallerThroughTheSharedPipeline()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "alice")], "test"));
        inboundCaller.Setup(x => x.IsInboundRequest).Returns(true);
        inboundCaller.Setup(x => x.Principal).Returns(principal);
        inboundCaller.Setup(x => x.Credential).Returns("Bearer alice-inbound");

        await CreateSut().QuerySourceAsync<FakeView>("My-Context", new QueryRequest(), Cancel);

        var call = Assert.Single(sent).Call;
        Assert.Equal(KaleidoCallPurpose.OnBehalfOfCaller, call.Purpose);
        Assert.Same(principal, call.Caller);
        Assert.Equal("Bearer alice-inbound", call.CallerAuthorization);
    }

    [Fact]
    public async Task QuerySourceAsync_AForbiddenRemoteAnswer_PreservesItsError()
    {
        responseStatus = HttpStatusCode.Forbidden;

        var failure = await Assert.ThrowsAsync<KaleidoHttpClientException>(() => CreateSut().QuerySourceAsync<FakeView>("My-Context", new QueryRequest(), Cancel));

        Assert.Equal(HttpStatusCode.Forbidden, failure.StatusCode);
        Assert.Equal("qry_forbidden", Assert.Single(failure.Errors).Code);
    }

    public void Dispose()
    {
        foreach (var client in created)
        {
            client.Dispose();
        }
    }
}
