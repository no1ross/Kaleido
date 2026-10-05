using Kaleido.Http.Client;
using Kaleido.Http.Queryable;
using Kaleido.Observability;
using Kaleido.Http.FunctionalTests.Queryable.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.FunctionalTests.Queryable.Client;

/// <summary>
/// Validates that <see cref="IKaleidoQueryableClientFactory"/> and its dependencies are correctly
/// wired through DI (no captive-dependency lifetime errors) and that outbound requests carry the
/// expected correlation headers.
/// </summary>
public sealed class QueryableClientHeaderTests : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly QueryableAspNetCoreFixture _fixture;

    public QueryableClientHeaderTests(QueryableAspNetCoreFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------------------------------------------------------------------------
    // DI lifetime validation
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddQueryableClient_ValidateOnBuild_DoesNotThrowCaptiveDependencyError()
    {
        var services = BuildClientServices(new KaleidoCorrelationContext());

        // Must not throw AggregateException for captive dependency
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>();
        Assert.NotNull(factory);
    }

    // ---------------------------------------------------------------------------
    // Correlation header propagation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_InsideStep_StampsEndToEndAndCallingProcessorStep()
    {
        var processId = Guid.NewGuid();
        var ctx = new KaleidoCorrelationContext
        {
            RequestId            = "req-queryable-header-test",
            ProcessId            = processId,
            CallingProcessorName = "upstream",
            CallingStepName      = "upstream-step",
            ExecutingStepName    = "validate",
        };

        var (factory, captured) = BuildClientFactory(ctx);

        await factory.GetClient("test").GetRegistryAsync();

        var request = Assert.Single(captured);
        Assert.Equal("req-queryable-header-test",
            request.Headers.GetValues(KaleidoCorrelationHeaders.RequestId).First());
        Assert.Equal(processId.ToString(),
            request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId).First());
        // per-hop: this service and its executing step, never the inbound caller
        Assert.Equal("test-queryable-client",
            request.Headers.GetValues(KaleidoCorrelationHeaders.CallingProcessor).First());
        Assert.Equal("validate",
            request.Headers.GetValues(KaleidoCorrelationHeaders.CallingStep).First());
    }

    [Fact]
    public async Task GetRegistryAsync_OutsideStep_ForwardsEndToEndOnly()
    {
        var ctx = new KaleidoCorrelationContext
        {
            RequestId            = "req-queryable-header-test",
            CallingProcessorName = "upstream",
            CallingStepName      = "upstream-step",
        };

        var (factory, captured) = BuildClientFactory(ctx);

        await factory.GetClient("test").GetRegistryAsync();

        var request = Assert.Single(captured);
        Assert.Equal("req-queryable-header-test",
            request.Headers.GetValues(KaleidoCorrelationHeaders.RequestId).First());
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingProcessor));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingStep));
    }

    [Fact]
    public async Task GetRegistryAsync_WhenCorrelationContextIsEmpty_StampsNoHeaders()
    {
        var (factory, captured) = BuildClientFactory(new KaleidoCorrelationContext());

        await factory.GetClient("test").GetRegistryAsync();

        var request = Assert.Single(captured);
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.RequestId));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.ProcessId));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingProcessor));
        Assert.False(request.Headers.Contains(KaleidoCorrelationHeaders.CallingStep));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private (IKaleidoQueryableClientFactory factory, List<HttpRequestMessage> captured)
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
        var factory = scope.ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>();
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
        // Client container needs no type scanning; under xUnit v3 the entry assembly
        // is this test project (it was testhost under v2), so the fallback would
        // register functional test sources without their dependencies.
        services.AddKaleido(config, o =>
                {
                    o.ServiceName = "test-queryable-client";
                    o.Assemblies = [typeof(KaleidoServiceOptions).Assembly];
                })
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
