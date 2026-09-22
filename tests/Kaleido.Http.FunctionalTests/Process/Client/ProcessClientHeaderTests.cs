using Kaleido.Http.Client;
using Kaleido.Http.Process;
using Kaleido.Observability;
using Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Client;

/// <summary>
/// Validates that <see cref="IKaleidoProcessClientFactory"/> and its dependencies are correctly
/// wired through DI (no captive-dependency lifetime errors) and that outbound requests carry the
/// expected correlation headers.
/// </summary>
public sealed class ProcessClientHeaderTests : IClassFixture<ProcessAspNetCoreFixture>
{
    private readonly ProcessAspNetCoreFixture _fixture;

    public ProcessClientHeaderTests(ProcessAspNetCoreFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------------------------------------------------------------------------
    // DI lifetime validation
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddProcessClient_ValidateOnBuild_DoesNotThrowCaptiveDependencyError()
    {
        var services = BuildClientServices(new KaleidoCorrelationContext());

        // Must not throw AggregateException for captive dependency
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IKaleidoProcessClientFactory>();
        Assert.NotNull(factory);
    }

    // ---------------------------------------------------------------------------
    // Correlation header propagation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_StampsAllCorrelationHeadersOnRequest()
    {
        var processId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var ctx = new KaleidoCorrelationContext
        {
            RequestId           = "req-process-header-test",
            ProcessId           = processId,
            ProcessorInstanceId = instanceId,
            SourceProcessorName = "intake",
            StepName            = "validate",
        };

        var (factory, captured) = BuildClientFactory(ctx);

        await factory.GetClient("test").GetRegistryAsync();

        var request = Assert.Single(captured);
        Assert.Equal("req-process-header-test",
            request.Headers.GetValues(KaleidoCorrelationHeaders.RequestId).First());
        Assert.Equal(processId.ToString(),
            request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId).First());
        Assert.Equal(instanceId.ToString(),
            request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessorInstanceId).First());
        Assert.Equal("intake",
            request.Headers.GetValues(KaleidoCorrelationHeaders.SourceProcessor).First());
        Assert.Equal("validate",
            request.Headers.GetValues(KaleidoCorrelationHeaders.StepName).First());
    }

    [Fact]
    public async Task GetRegistryAsync_WhenCorrelationContextIsEmpty_StampsNoHeaders()
    {
        var (factory, captured) = BuildClientFactory(new KaleidoCorrelationContext());

        await factory.GetClient("test").GetRegistryAsync();

        var request = Assert.Single(captured);
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.RequestId));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.ProcessId));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.ProcessorInstanceId));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.SourceProcessor));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.StepName));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private (IKaleidoProcessClientFactory factory, List<HttpRequestMessage> captured)
        BuildClientFactory(KaleidoCorrelationContext ctx)
    {
        var captured = new List<HttpRequestMessage>();
        var captureHandler = new CaptureHandler(captured, _fixture.TestServer.CreateHandler());
        var httpClient = new HttpClient(captureHandler) { BaseAddress = new Uri("http://localhost/") };

        var services = BuildClientServices(ctx);
        services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory("test", httpClient));

        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IKaleidoProcessClientFactory>();
        return (factory, captured);
    }

    private static ServiceCollection BuildClientServices(KaleidoCorrelationContext ctx)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:Clients:test:BaseUrl"]     = "http://localhost/",
                ["Kaleido:Clients:test:RoutePrefix"] = "kaleido",
            })
            .Build();

        var services = new ServiceCollection();
        // Register before AddKaleido so TryAddScoped is skipped and this accessor wins
        services.AddSingleton<IKaleidoCorrelationContextAccessor>(
            new FixedCorrelationContextAccessor(ctx));
        services.AddKaleido(config, o => o.ServiceName = "test-client")
                .AddHttpClients();
        return services;
    }

    private sealed class FixedCorrelationContextAccessor(KaleidoCorrelationContext context)
        : IKaleidoCorrelationContextAccessor
    {
        public KaleidoCorrelationContext Current => context;
    }

    private sealed class FixedHttpClientFactory(string name, HttpClient client)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string n) =>
            n == name ? client : throw new InvalidOperationException($"Unknown client name: {n}");
    }

    private sealed class CaptureHandler(List<HttpRequestMessage> captured, HttpMessageHandler inner)
        : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            captured.Add(request);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
