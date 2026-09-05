using Kaleido.Observability;
using Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Process.AspNetCore.FunctionalTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Client;

/// <summary>
/// Verifies that <see cref="IKaleidoProcessClient"/> stamps the correct
/// <c>X-Kaleido-*</c> correlation headers on outbound requests, that
/// <see cref="IKaleidoProcessClient.GetRegistryAsync"/> does not stamp any,
/// and that the server writes the expected response headers after step execution.
/// </summary>
[Collection(nameof(ProcessAspNetCoreCollection))]
public sealed class ProcessClientHeaderTests : IDisposable
{
    private readonly ServiceProvider _clientProvider;
    private readonly IKaleidoProcessClientFactory _factory;
    private readonly CaptureState _captureState;
    private readonly SettableCorrelationContextAccessor _correlationAccessor;
    private readonly HttpClient _rawClient;
    private readonly HttpClient _httpClient;

    public ProcessClientHeaderTests(ProcessAspNetCoreFixture fixture)
    {
        _rawClient = fixture.Client;

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
            .AddProcessClient(o =>
            {
                o.Name = "header-test";
                o.BaseUrl = "http://localhost/";
                o.RoutePrefix = "kaleido";
            });

        // Override the accessor registered by AddKaleido() so the client uses our
        // controllable context. Replace() removes the previous registration first.
        services.Replace(ServiceDescriptor.Singleton<IKaleidoCorrelationContextAccessor>(_correlationAccessor));

        _clientProvider = services.BuildServiceProvider();
        _factory = _clientProvider.GetRequiredService<IKaleidoProcessClientFactory>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _clientProvider.Dispose();
    }

    // ---------------------------------------------------------------------------
    // Outbound: methods that should stamp all four headers
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_StampsAllCorrelationHeaders()
    {
        var context = new KaleidoCorrelationContext
        {
            RequestId = "req-ex-01",
            ProcessId = Guid.Parse("11111111-0000-0000-0000-000000000001"),
            ProcessorInstanceId = Guid.Parse("22222222-0000-0000-0000-000000000001"),
            SourceProcessorName = "intake"
        };
        _correlationAccessor.Current = context;

        await _factory.GetClient("header-test").ExecuteStepAsync(new RuntimeRootStep());

        AssertAllCorrelationHeaders(_captureState.LastRequestHeaders, context);
    }

    [Fact]
    public async Task GetProcessStateAsync_StampsAllCorrelationHeaders()
    {
        var context = new KaleidoCorrelationContext
        {
            RequestId = "req-ps-01",
            ProcessId = Guid.Parse("11111111-0000-0000-0000-000000000002"),
            ProcessorInstanceId = Guid.Parse("22222222-0000-0000-0000-000000000002"),
            SourceProcessorName = "intake"
        };
        _correlationAccessor.Current = context;

        await _factory.GetClient("header-test").GetProcessStateAsync(Guid.NewGuid());

        AssertAllCorrelationHeaders(_captureState.LastRequestHeaders, context);
    }

    [Fact]
    public async Task GetStepMetadataAsync_StampsAllCorrelationHeaders()
    {
        var context = new KaleidoCorrelationContext
        {
            RequestId = "req-sm-01",
            ProcessId = Guid.Parse("11111111-0000-0000-0000-000000000003"),
            ProcessorInstanceId = Guid.Parse("22222222-0000-0000-0000-000000000003"),
            SourceProcessorName = "intake"
        };
        _correlationAccessor.Current = context;

        await _factory.GetClient("header-test").GetStepMetadataAsync(RuntimeStepNames.Root);

        AssertAllCorrelationHeaders(_captureState.LastRequestHeaders, context);
    }

    // ---------------------------------------------------------------------------
    // Outbound: GetRegistryAsync should NOT stamp correlation headers
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
    // Inbound: server writes response headers after step execution
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_ResponseContainsProcessIdHeader()
    {
        using var response = await _rawClient.PostAsJsonAsync(
            "/kaleido/processes/steps/runtimeroot",
            new ExecuteStepRequest<RuntimeRootStep> { ProcessStep = new RuntimeRootStep() });

        Assert.True(response.IsSuccessStatusCode);
        Assert.True(response.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessId, out var values));
        Assert.True(Guid.TryParse(values!.First(), out _));
    }

    [Fact]
    public async Task ExecuteStepAsync_ResponseContainsProcessorInstanceIdHeader()
    {
        using var response = await _rawClient.PostAsJsonAsync(
            "/kaleido/processes/steps/runtimeroot",
            new ExecuteStepRequest<RuntimeRootStep> { ProcessStep = new RuntimeRootStep() });

        Assert.True(response.IsSuccessStatusCode);
        Assert.True(response.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessorInstanceId, out var values));
        Assert.True(Guid.TryParse(values!.First(), out _));
    }

    [Fact]
    public async Task ExecuteStepAsync_ResponseContainsSourceProcessorHeader()
    {
        using var response = await _rawClient.PostAsJsonAsync(
            "/kaleido/processes/steps/runtimeroot",
            new ExecuteStepRequest<RuntimeRootStep> { ProcessStep = new RuntimeRootStep() });

        Assert.True(response.IsSuccessStatusCode);
        Assert.True(response.Headers.TryGetValues(KaleidoCorrelationHeaders.SourceProcessor, out var values));
        Assert.Equal(FunctionalProcessorNames.TestProcessor, values!.First());
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
