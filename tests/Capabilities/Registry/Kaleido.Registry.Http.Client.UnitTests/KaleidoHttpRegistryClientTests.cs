using System.Net.Http.Headers;
using System.Security.Claims;

namespace Kaleido.Registry.Http.Client.UnitTests;

public sealed class KaleidoHttpRegistryClientTests : Kaleido.UnitTests.SutFixture, IDisposable
{
    private static readonly Guid InstanceId = Guid.Parse("693c0aa9-48c5-44c3-978a-aa7aa681d0a1");
    private readonly Mock<HttpMessageHandler> handler = new(MockBehavior.Strict);
    private readonly Mock<IKaleidoInboundCallerAccessor> inbound = new();
    private readonly Mock<IKaleidoCorrelationContextAccessor> correlation = new();
    private readonly List<HttpRequestMessage> requests = [];
    private readonly List<HttpClient> clients = [];
    private Func<HttpRequestMessage, HttpResponseMessage> answer = _ => new HttpResponseMessage(HttpStatusCode.NoContent);

    public KaleidoHttpRegistryClientTests()
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
    }

    private KaleidoHttpRegistryClient CreateSut()
    {
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                requests.Add(request);
                return answer(request);
            });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(KaleidoHttpRegistryClient.ClientName)).Returns(() =>
        {
            var client = new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("https://gateway.test/") };
            clients.Add(client);
            return client;
        });
        return new KaleidoHttpRegistryClient(factory.Object, inbound.Object, correlation.Object, new KaleidoServiceOptions { ServiceName = "reader-service" });
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static HttpResponseMessage Ok<T>(T body, string etag) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options),
            Headers = { ETag = new EntityTagHeaderValue(etag) },
        };

    public void Dispose()
    {
        foreach (var client in clients)
        {
            client.Dispose();
        }

    }

    [Theory]
    [InlineData("registry", "/registry")]
    [InlineData("capabilities", "/registry/capabilities")]
    [InlineData("availability", "/registry/availability")]
    public async Task ARead_UsesTheFixedRegistryPath_AndConditionalETag(string document, string path)
    {
        answer = _ => document == "availability"
            ? Ok(new KaleidoRegistryAvailability(), "\"rev-2\"")
            : Ok(new KaleidoRegistry(), "\"rev-2\"");
        var sut = CreateSut();

        var result = document switch
        {
            "registry" => (object)await sut.GetRegistryAsync("\"rev-1\"", Cancel),
            "capabilities" => await sut.GetCapabilitiesAsync("\"rev-1\"", Cancel),
            _ => await sut.GetAvailabilityAsync("\"rev-1\"", Cancel),
        };

        Assert.Equal(path, Assert.Single(requests).RequestUri?.AbsolutePath);
        Assert.Equal("\"rev-1\"", Assert.Single(requests).Headers.IfNoneMatch.Single().ToString());
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ARegistryRead_ForwardsEndToEndCorrelationButNotTheInboundCallingStep()
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext
        {
            RequestId = "request-123",
            Origin = "mcp",
            OriginTool = "search",
            ProcessId = InstanceId,
            CallingProcessorName = "previous-service",
            CallingStepName = "previous-step",
        });
        answer = _ => Ok(new KaleidoRegistry(), "\"rev\"");

        await CreateSut().GetCapabilitiesAsync(null, Cancel);

        var request = Assert.Single(requests);
        Assert.Equal("request-123", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.RequestId)));
        Assert.Equal("mcp", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.Origin)));
        Assert.Equal("search", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.OriginTool)));
        Assert.Equal(InstanceId.ToString("D"), Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingProcessor));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingStep));
    }

    [Fact]
    public async Task TheHostCopy_ReadsWithItsOwnCredential_AndKeepsAnUnchangedDocument()
    {
        inbound.Setup(x => x.IsInboundRequest).Returns(true);
        inbound.Setup(x => x.Credential).Returns("Bearer caller");
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext
        {
            ExecutingStepName = "lookup-step",
            CallingProcessorName = "untrusted-inbound",
            CallingStepName = "old-step",
        });
        answer = _ => new HttpResponseMessage(HttpStatusCode.NotModified);
        var sut = CreateSut();

        var result = await sut.GetCapabilitiesAsync("\"rev-1\"", Cancel);

        Assert.True(result.NotModified);
        Assert.Null(result.Value);
        Assert.Equal("\"rev-1\"", result.ETag);
        var request = Assert.Single(requests);
        Assert.Equal(KaleidoCallPurpose.ServiceOwn, KaleidoCallContext.Of(request).Purpose);
        Assert.Equal("reader-service", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.CallingProcessor)));
        Assert.Equal("lookup-step", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.CallingStep)));
    }

    [Fact]
    public async Task ACallerFilteredRead_CarriesTheCaller_WithoutChangingTheHostCopy()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "bob")], "test"));
        inbound.Setup(x => x.IsInboundRequest).Returns(true);
        inbound.Setup(x => x.Principal).Returns(user);
        inbound.Setup(x => x.Credential).Returns("Bearer caller");
        answer = _ => Ok(new KaleidoRegistry(), "\"rev-1\"");
        var sut = CreateSut();

        var result = await sut.GetRegistryAsync(null, Cancel);

        Assert.NotNull(result.Value);
        Assert.False(result.NotModified);
        Assert.Equal("\"rev-1\"", result.ETag);
        var call = KaleidoCallContext.Of(Assert.Single(requests));
        Assert.Equal(KaleidoCallPurpose.OnBehalfOfCaller, call.Purpose);
        Assert.Same(user, call.Caller);
        Assert.Equal("Bearer caller", call.CallerAuthorization);
    }

    [Fact]
    public async Task Registration_Heartbeat_AndDeregistration_UseTheRegistryPaths_AsTheService()
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { RequestId = "service-request" });
        answer = request => request.Method == HttpMethod.Delete
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Ok(new RegistryLease { InstanceId = InstanceId, HeartbeatInterval = TimeSpan.FromSeconds(10), LeaseUntilUtc = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero) }, "\"lease\"");
        var sut = CreateSut();

        var registered = await sut.RegisterAsync(new RegistryInstanceRegistration { ServiceName = "intake", ServiceVersion = "1", BuildVersion = "1.0.0-preview.616", InstanceId = InstanceId }, Cancel);
        var renewed = await sut.HeartbeatAsync(InstanceId, new RegistryHeartbeat { Healthy = true }, Cancel);
        await sut.DeregisterAsync(InstanceId, Cancel);

        Assert.Equal(InstanceId, registered.InstanceId);
        Assert.Equal(InstanceId, renewed?.InstanceId);
        Assert.Equal([(HttpMethod.Post, "/registry/instances"), (HttpMethod.Put, $"/registry/instances/{InstanceId:D}/heartbeat"), (HttpMethod.Delete, $"/registry/instances/{InstanceId:D}")],
            requests.Select(r => (r.Method, r.RequestUri?.AbsolutePath)));
        Assert.All(requests, request =>
        {
            Assert.Equal(KaleidoCallPurpose.ServiceOwn, KaleidoCallContext.Of(request).Purpose);
            Assert.Equal("service-request", Assert.Single(request.Headers.GetValues(KaleidoCorrelationHeaders.RequestId)));
        });
    }

    [Fact]
    public async Task AHeartbeatForAnUnknownInstance_ReturnsNull()
    {
        answer = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        Assert.Null(await CreateSut().HeartbeatAsync(InstanceId, new RegistryHeartbeat(), Cancel));
    }

    [Fact]
    public async Task ARegistryReadWithoutADocument_IsARegistryClientFailure()
    {
        answer = _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().GetCapabilitiesAsync(null, Cancel));

        Assert.Equal(RegistryClientErrorCodes.EmptyResponse, exception.Code);
        Assert.Equal("204", exception.Status);
    }

    [Fact]
    public async Task AnInvalidRegistryDocument_IsARegistryClientFailure()
    {
        answer = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{invalid") };

        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().GetRegistryAsync(null, Cancel));

        Assert.Equal(RegistryClientErrorCodes.InvalidResponse, exception.Code);
        Assert.Equal("200", exception.Status);
        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task AnUnreachableRegistry_IsARegistryClientFailureWithoutATransportStatus()
    {
        answer = _ => throw new HttpRequestException("offline");

        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().GetAvailabilityAsync(null, Cancel));

        Assert.Equal(RegistryClientErrorCodes.RequestFailed, exception.Code);
        Assert.Null(exception.Status);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task AFailedRegistryRead_ReportsItsStatus()
    {
        answer = _ => new HttpResponseMessage(HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().GetAvailabilityAsync(null, Cancel));

        Assert.Equal("403", exception.Status);
        Assert.Equal(RegistryClientErrorCodes.RequestFailed, exception.Code);
    }
}
