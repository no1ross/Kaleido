using Kaleido.Observability;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Client;

/// <summary>
/// Verifies that <see cref="IKaleidoQueryableClient"/> stamps the correct
/// <c>X-Kaleido-*</c> correlation headers on outbound requests, and that
/// <see cref="IKaleidoQueryableClient.GetRegistryAsync"/> does not stamp any.
/// </summary>
public sealed class QueryableClientHeaderTests : IClassFixture<QueryableAspNetCoreFixture>, IDisposable
{
    private readonly ServiceProvider _clientProvider;
    private readonly IKaleidoQueryableClientFactory _factory;
    private readonly CaptureState _captureState;
    private readonly SettableCorrelationContextAccessor _correlationAccessor;
    private readonly HttpClient _httpClient;

    public QueryableClientHeaderTests(QueryableAspNetCoreFixture fixture)
    {
        var testHandler = fixture.TestServer.CreateHandler();

        _captureState = new CaptureState();
        _correlationAccessor = new SettableCorrelationContextAccessor();

        // Build the pipeline once: capture handler wraps the TestServer handler.
        // Registering a FixedHttpClientFactory bypasses the IHttpClientBuilder
        // pipeline system and guarantees the capture handler is always in the chain.
        var captureHandler = new RequestCaptureHandler(_captureState) { InnerHandler = testHandler };
        _httpClient = new HttpClient(captureHandler) { BaseAddress = new Uri("http://localhost/") };

        var services = new ServiceCollection();
        services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory("header-test", _httpClient));

        services.AddKaleido()
            .AddQueryableClient(o =>
            {
                o.Name = "header-test";
                o.BaseUrl = "http://localhost/";
                o.RoutePrefix = "kaleido";
            });

        // Override the accessor registered by AddKaleido() so the client uses our
        // controllable context. Replace() removes the previous registration first.
        services.Replace(ServiceDescriptor.Singleton<IKaleidoCorrelationContextAccessor>(_correlationAccessor));

        _clientProvider = services.BuildServiceProvider();
        _factory = _clientProvider.GetRequiredService<IKaleidoQueryableClientFactory>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _clientProvider.Dispose();
    }

    // ---------------------------------------------------------------------------
    // Methods that should stamp all four headers
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QueryViewAsync_StampsAllCorrelationHeaders()
    {
        var context = new KaleidoCorrelationContext
        {
            RequestId = "req-qv-01",
            ProcessId = Guid.Parse("11111111-0000-0000-0000-000000000001"),
            ProcessorInstanceId = Guid.Parse("22222222-0000-0000-0000-000000000001"),
            SourceProcessorName = "intake"
        };
        _correlationAccessor.Current = context;

        await _factory.GetClient("header-test").QueryViewAsync<FunctionalRecordViewParameters, FunctionalRecordView>(
            "functional-records",
            "grid",
            new QueryApiRequest<FunctionalRecordViewParameters>(
                new FunctionalRecordViewParameters { Category = "Alpha" },
                new QueryBody()));

        AssertAllCorrelationHeaders(_captureState.LastRequestHeaders, context);
    }

    [Fact]
    public async Task QueryContextAsync_StampsAllCorrelationHeaders()
    {
        var context = new KaleidoCorrelationContext
        {
            RequestId = "req-qc-01",
            ProcessId = Guid.Parse("11111111-0000-0000-0000-000000000002"),
            ProcessorInstanceId = Guid.Parse("22222222-0000-0000-0000-000000000002"),
            SourceProcessorName = "intake"
        };
        _correlationAccessor.Current = context;

        await _factory.GetClient("header-test").QueryContextAsync<FunctionalRecordContext>(
            "functional-records",
            new QueryApiRequest(new QueryBody()));

        AssertAllCorrelationHeaders(_captureState.LastRequestHeaders, context);
    }

    [Fact]
    public async Task GetContextMetadataAsync_StampsAllCorrelationHeaders()
    {
        var context = new KaleidoCorrelationContext
        {
            RequestId = "req-md-01",
            ProcessId = Guid.Parse("11111111-0000-0000-0000-000000000003"),
            ProcessorInstanceId = Guid.Parse("22222222-0000-0000-0000-000000000003"),
            SourceProcessorName = "intake"
        };
        _correlationAccessor.Current = context;

        await _factory.GetClient("header-test").GetContextMetadataAsync("functional-records");

        AssertAllCorrelationHeaders(_captureState.LastRequestHeaders, context);
    }

    // ---------------------------------------------------------------------------
    // GetRegistryAsync should NOT stamp correlation headers
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_DoesNotStampCorrelationHeaders()
    {
        _correlationAccessor.Current = new KaleidoCorrelationContext
        {
            RequestId = "req-reg-01",
            ProcessId = Guid.NewGuid(),
            ProcessorInstanceId = Guid.NewGuid(),
            SourceProcessorName = "intake"
        };

        await _factory.GetClient("header-test").GetRegistryAsync();

        var headers = _captureState.LastRequestHeaders;
        Assert.False(headers.ContainsKey(KaleidoCorrelationHeaders.RequestId));
        Assert.False(headers.ContainsKey(KaleidoCorrelationHeaders.ProcessId));
        Assert.False(headers.ContainsKey(KaleidoCorrelationHeaders.ProcessorInstanceId));
        Assert.False(headers.ContainsKey(KaleidoCorrelationHeaders.SourceProcessor));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static void AssertAllCorrelationHeaders(
        IReadOnlyDictionary<string, string?> headers,
        KaleidoCorrelationContext expected)
    {
        Assert.True(headers.TryGetValue(KaleidoCorrelationHeaders.RequestId, out var requestId),
            $"RequestId header missing. Captured: [{string.Join(", ", headers.Keys)}]");
        Assert.Equal(expected.RequestId, requestId);

        Assert.True(headers.TryGetValue(KaleidoCorrelationHeaders.ProcessId, out var processId));
        Assert.Equal(expected.ProcessId!.Value.ToString(), processId);

        Assert.True(headers.TryGetValue(KaleidoCorrelationHeaders.ProcessorInstanceId, out var instanceId));
        Assert.Equal(expected.ProcessorInstanceId!.Value.ToString(), instanceId);

        Assert.True(headers.TryGetValue(KaleidoCorrelationHeaders.SourceProcessor, out var sourceProcessor));
        Assert.Equal(expected.SourceProcessorName, sourceProcessor);
    }

    // ---------------------------------------------------------------------------
    // Test infrastructure
    // ---------------------------------------------------------------------------

    private sealed class SettableCorrelationContextAccessor : IKaleidoCorrelationContextAccessor
    {
        public KaleidoCorrelationContext Current { get; set; } = new();
    }

    /// <summary>
    /// Shared state updated by <see cref="RequestCaptureHandler"/> on every outbound request.
    /// </summary>
    private sealed class CaptureState
    {
        public IReadOnlyDictionary<string, string?> LastRequestHeaders { get; set; }
            = new Dictionary<string, string?>();
    }

    private sealed class RequestCaptureHandler : DelegatingHandler
    {
        private readonly CaptureState _state;

        public RequestCaptureHandler(CaptureState state) => _state = state;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _state.LastRequestHeaders = request.Headers
                .ToDictionary(h => h.Key, h => h.Value.FirstOrDefault());
            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// An <see cref="IHttpClientFactory"/> that returns a fixed, pre-wired
    /// <see cref="HttpClient"/> for a given named client.
    /// </summary>
    private sealed class FixedHttpClientFactory : IHttpClientFactory
    {
        private readonly string _name;
        private readonly HttpClient _client;

        public FixedHttpClientFactory(string name, HttpClient client)
        {
            _name = name;
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            if (string.Equals(name, _name, StringComparison.OrdinalIgnoreCase))
                return _client;

            throw new InvalidOperationException($"Unknown named client '{name}'.");
        }
    }
}
