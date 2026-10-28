using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.UnitTests;

// How a Kaleido client request is sent and its answer read (#498); what each client sends is tested
// in KaleidoProcessorClientTests and KaleidoQueryableClientTests.
public sealed class KaleidoClientTransportTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly KaleidoClientDefinition Radiology =
        new("RadiologyClient", "radiology", RegistryTtl: null, StrictRegistryProbe: false, HealthCheckTimeout: TimeSpan.FromSeconds(5));

    private readonly List<(HttpRequestMessage Request, string? Body)> sent = [];
    private readonly Mock<IHttpClientFactory> httpClientFactory = new();
    private readonly Mock<ICorrelationHeaderStamper> stamper = new();
    private readonly Mock<IKaleidoRemoteRegistry> remoteRegistry = new();

    private KaleidoClientTransport CreateSut(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        ILogger<KaleidoClientTransport>? logger = null)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                sent.Add((request, request.Content?.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult()));
                return respond(request);
            });
        httpClientFactory.Setup(x => x.CreateClient(Radiology.Name))
            .Returns(() => new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("http://localhost") });

        return new(httpClientFactory.Object, stamper.Object, remoteRegistry.Object, logger ?? NullLogger<KaleidoClientTransport>.Instance);
    }

    private static HttpResponseMessage JsonOk<T>(T value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value, options: KaleidoJsonOptions.Options) };

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    private static KaleidoClientRequest Get(string url = "/radiology/processes/1") =>
        new(Radiology, HttpMethod.Get, url, "Process state request");

    private static KaleidoClientRequest Post(object body) =>
        new(Radiology, HttpMethod.Post, "/radiology/processes/execute", "Process execute request", body);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private sealed record Answer(string Value);

    [Fact]
    public async Task SendAsync_SendsThroughTheClientsNamedHttpClient_AndReadsTheAnswer()
    {
        var sut = CreateSut(_ => JsonOk(new Answer("ok")));

        var answer = await sut.SendAsync<Answer>(Post(new { memberId = "M1" }), Cancel);

        Assert.Equal("ok", answer.Value);
        var (request, body) = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/radiology/processes/execute", request.RequestUri!.AbsolutePath);
        Assert.Equal("{\"memberId\":\"M1\"}", body);
        httpClientFactory.Verify(x => x.CreateClient("RadiologyClient"), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithoutABody_SendsNoContent()
    {
        var sut = CreateSut(_ => JsonOk(new Answer("ok")));

        await sut.SendAsync<Answer>(Get(), Cancel);

        Assert.Null(Assert.Single(sent).Body);
    }

    [Fact]
    public async Task SendAsync_StampsTheCorrelationHeaders()
    {
        var sut = CreateSut(_ => JsonOk(new Answer("ok")));

        await sut.SendAsync<Answer>(Get(), Cancel);

        stamper.Verify(x => x.Stamp(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_AnExplicitProcessId_WinsOverTheAmbientOne()
    {
        var explicitId = Guid.NewGuid();
        stamper.Setup(x => x.Stamp(It.IsAny<HttpRequestMessage>()))
            .Callback((HttpRequestMessage r) => r.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, Guid.NewGuid().ToString()));
        var sut = CreateSut(_ => JsonOk(new Answer("ok")));

        await sut.SendAsync<Answer>(Post(new { }) with { ProcessId = explicitId }, Cancel);

        Assert.Equal(explicitId.ToString(), Assert.Single(Assert.Single(sent).Request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
    }

    [Fact]
    public async Task SendAsync_WithoutAProcessId_KeepsTheAmbientOne()
    {
        var ambient = Guid.NewGuid().ToString();
        stamper.Setup(x => x.Stamp(It.IsAny<HttpRequestMessage>()))
            .Callback((HttpRequestMessage r) => r.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, ambient));
        var sut = CreateSut(_ => JsonOk(new Answer("ok")));

        await sut.SendAsync<Answer>(Post(new { }), Cancel);

        Assert.Equal(ambient, Assert.Single(Assert.Single(sent).Request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
    }

    // One key per idempotent call: a retry of the same message carries the same key, a new call a new one.
    [Fact]
    public async Task SendAsync_AnIdempotentRequest_GetsANewKeyWithEveryCall()
    {
        var sut = CreateSut(_ => JsonOk(new Answer("ok")));

        await sut.SendAsync<Answer>(Post(new { }) with { Idempotent = true }, Cancel);
        await sut.SendAsync<Answer>(Post(new { }) with { Idempotent = true }, Cancel);

        var keys = sent.Select(x => x.Request.Headers.GetValues(KaleidoIdempotencyHeaders.IdempotencyKey).Single()).ToArray();
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
        Assert.NotEqual(keys[0], keys[1]);
    }

    // #440: a caller's own key is sent as is; a blank key is no key (an idempotent call gets its own).
    [Theory]
    [InlineData("order-42-confirm", true)]
    [InlineData("order-42-confirm", false)]
    [InlineData("  ", true)]
    public async Task SendAsync_TheCallersKey_IsSentAsIs(string given, bool idempotent)
    {
        var sut = CreateSut(_ => JsonOk(new Answer("ok")));

        await sut.SendAsync<Answer>(Post(new { }) with { IdempotencyKey = given, Idempotent = idempotent }, Cancel);

        var key = Assert.Single(Assert.Single(sent).Request.Headers.GetValues(KaleidoIdempotencyHeaders.IdempotencyKey));
        Assert.Equal(string.IsNullOrWhiteSpace(given) ? key : given, key);
        Assert.False(string.IsNullOrWhiteSpace(key));
    }

    [Fact]
    public async Task SendAsync_ARequestThatIsNotIdempotent_SendsNoKey()
    {
        var sut = CreateSut(_ => JsonOk(new Answer("ok")));

        await sut.SendAsync<Answer>(Get(), Cancel);

        Assert.False(Assert.Single(sent).Request.Headers.Contains(KaleidoIdempotencyHeaders.IdempotencyKey));
    }

    [Fact]
    public async Task SendAsync_AFailedCall_ThrowsTheMappedFailureWithTheDescription()
    {
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(
                new KaleidoErrorResponse([new KaleidoError("pro_invalid_step", "Field required.")]),
                options: KaleidoJsonOptions.Options),
        });

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.SendAsync<Answer>(Post(new { }), Cancel));

        Assert.Equal(HttpClientErrorCodes.ValidationFailed, exception.Code);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("pro_invalid_step", Assert.Single(exception.Errors).Code);
        Assert.Equal("Process execute request failed: Field required.", exception.Message);
    }

    [Fact]
    public async Task SendAsync_ANotFound_IsAFailure()
    {
        var sut = CreateSut(_ => Status(HttpStatusCode.NotFound));

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.SendAsync<Answer>(Get(), Cancel));

        Assert.Equal(HttpClientErrorCodes.RequestFailed, exception.Code);
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task SendAsync_ASuccessWithoutAPayload_ThrowsEmptyResponse()
    {
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json"),
        });

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.SendAsync<Answer>(Get(), Cancel));

        Assert.Equal(HttpClientErrorCodes.EmptyResponse, exception.Code);
        Assert.Equal("Process state request succeeded but returned no payload.", exception.Message);
    }

    [Fact]
    public async Task SendOrNotFoundAsync_ANotFound_IsNull()
    {
        var sut = CreateSut(_ => Status(HttpStatusCode.NotFound));

        Assert.Null(await sut.SendOrNotFoundAsync<Answer>(Get(), Cancel));
    }

    [Fact]
    public async Task SendOrNotFoundAsync_AnAnswer_IsRead()
    {
        var sut = CreateSut(_ => JsonOk(new Answer("found")));

        Assert.Equal("found", (await sut.SendOrNotFoundAsync<Answer>(Get(), Cancel))?.Value);
    }

    [Fact]
    public async Task SendOrNotFoundAsync_AnyOtherFailure_Throws()
    {
        var sut = CreateSut(_ => Status(HttpStatusCode.ServiceUnavailable));

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.SendOrNotFoundAsync<Answer>(Get(), Cancel));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    // A capability call answered 404: the service does not have that capability, so the discovery
    // snapshot is dropped. Other calls (a process state) and other failures leave it alone.
    [Theory]
    [InlineData(true, HttpStatusCode.NotFound, 1)]
    [InlineData(false, HttpStatusCode.NotFound, 0)]
    [InlineData(true, HttpStatusCode.Forbidden, 0)]
    public async Task SendAsync_ANotFoundCapability_DropsTheRegistrySnapshot(bool invalidates, HttpStatusCode status, int invalidations)
    {
        var sut = CreateSut(_ => Status(status));

        await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.SendAsync<Answer>(Post(new { }) with { InvalidatesRegistryOnNotFound = invalidates }, Cancel));

        remoteRegistry.Verify(x => x.InvalidateAsync("RadiologyClient", Cancel), Times.Exactly(invalidations));
    }

    [Fact]
    public async Task SendAsync_LogsTheOutgoingCallAtDebug()
    {
        var logger = new RecordingLogger();
        var sut = CreateSut(_ => JsonOk(new Answer("ok")), logger);

        await sut.SendAsync<Answer>(Post(new { }), Cancel);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("Sending POST /radiology/processes/execute through Kaleido client 'RadiologyClient' to service 'radiology'.", entry.Message);
    }

    [Fact]
    public async Task SendAsync_WhenTheServiceFails_LogsAWarningWithTheStatus()
    {
        var logger = new RecordingLogger();
        var sut = CreateSut(_ => Status(HttpStatusCode.ServiceUnavailable), logger);

        await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.SendAsync<Answer>(Get(), Cancel));

        Assert.Collection(
            logger.Entries,
            sending => Assert.Equal(LogLevel.Debug, sending.Level),
            failed =>
            {
                Assert.Equal(LogLevel.Warning, failed.Level);
                Assert.Equal("Remote Kaleido service 'radiology' returned 503 (ServiceUnavailable) for GET /radiology/processes/1.", failed.Message);
            });
    }

    private sealed class RecordingLogger : ILogger<KaleidoClientTransport>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
