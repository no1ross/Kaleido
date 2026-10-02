using Kaleido.Exceptions;
using Kaleido.Http.Queryable;
using Kaleido.Http.Registry.Contracts;
using Kaleido.Queryable.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoEndpointRouteBuilderExtensionsTests
    : SutFixture
{
    [Fact]
    public void MapKaleidoHttp_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KaleidoEndpointRouteBuilderExtensions.MapKaleidoHttp(null!));
    }

    [Fact]
    public void MapKaleidoHttp_ReturnsIEndpointConventionBuilder()
    {
        var endpoints = CreateProcessAndQueryableEndpoints();

        var result = endpoints.MapKaleidoHttp();

        Assert.NotNull(result);
    }

    [Fact]
    public void MapKaleidoHttp_WithProcessOnly_MapsProcessEndpoints()
    {
        var endpoints = CreateProcessOnlyEndpoints();

        endpoints.MapKaleidoHttp();

        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ProcessEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, RegistryEndpointNames.RegistryEndpointName));
        Assert.Null(FindEndpoint(endpoints, QueryableEndpointNames.QueryContextMetadataEndpointName("test-context")));
    }

    [Fact]
    public void MapKaleidoHttp_WithQueryableOnly_MapsQueryableEndpoints()
    {
        var endpoints = CreateQueryableOnlyEndpoints();

        endpoints.MapKaleidoHttp();

        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QueryContextMetadataEndpointName("test-context")));
        Assert.NotNull(FindEndpoint(endpoints, RegistryEndpointNames.RegistryEndpointName));
        Assert.Null(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
    }

    [Fact]
    public void MapKaleidoHttp_WithBoth_MapsBothEndpoints()
    {
        var endpoints = CreateProcessAndQueryableEndpoints();

        endpoints.MapKaleidoHttp();

        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QueryContextMetadataEndpointName("test-context")));
        Assert.NotNull(FindEndpoint(endpoints, RegistryEndpointNames.RegistryEndpointName));
    }

    [Fact]
    public void MapKaleidoHttp_WithNeither_DoesNotThrowAndMapsNoKaleidoEndpoints()
    {
        var endpoints = CreateEmptyEndpoints();

        var result = endpoints.MapKaleidoHttp();

        Assert.NotNull(result);
        Assert.Null(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
        Assert.Null(FindEndpoint(endpoints, RegistryEndpointNames.RegistryEndpointName));
    }

    [Fact]
    public void MapKaleidoHttp_WithAggregateRegistryWithoutClients_Throws()
    {
        var endpoints = CreateEmptyEndpoints();

        Assert.Throws<KaleidoConfigurationException>(() =>
            endpoints.MapKaleidoHttp(o => o.AggregateRegistry = true));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static RouteEndpoint? FindEndpoint(IEndpointRouteBuilder endpoints, string name) =>
        endpoints.DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .SingleOrDefault(x =>
                x.Metadata
                    .OfType<IEndpointNameMetadata>()
                    .Any(m => string.Equals(m.EndpointName, name, StringComparison.Ordinal)));

    private static WebApplication CreateEmptyEndpoints(string serviceName = "test")
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName });
        return builder.Build();
    }

    private static WebApplication CreateProcessOnlyEndpoints(string serviceName = "test")
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName });
        builder.Services.AddSingleton<IProcessExecutionService>(Mock.Of<IProcessExecutionService>());
        builder.Services.AddSingleton<IProcessStateService>(Mock.Of<IProcessStateService>());
        builder.Services.AddSingleton<IProcessStepRegistry>(CreateProcessStepRegistry());
        builder.Services.AddSingleton<IProcessRegistry>(CreateProcessRegistry());
        return builder.Build();
    }

    private static WebApplication CreateQueryableOnlyEndpoints(string serviceName = "test")
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName });
        builder.Services.AddSingleton(Mock.Of<IQueryableService>());
        builder.Services.AddSingleton<IQueryableRegistry>(CreateQueryableRegistry());
        return builder.Build();
    }

    private static WebApplication CreateProcessAndQueryableEndpoints(string serviceName = "test")
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName });
        builder.Services.AddSingleton<IProcessExecutionService>(Mock.Of<IProcessExecutionService>());
        builder.Services.AddSingleton<IProcessStateService>(Mock.Of<IProcessStateService>());
        builder.Services.AddSingleton<IProcessStepRegistry>(CreateProcessStepRegistry());
        builder.Services.AddSingleton<IProcessRegistry>(CreateProcessRegistry());
        builder.Services.AddSingleton(Mock.Of<IQueryableService>());
        builder.Services.AddSingleton<IQueryableRegistry>(CreateQueryableRegistry());
        return builder.Build();
    }

    // ── Process registry helpers ─────────────────────────────────────────────

    private static IProcessStepRegistry CreateProcessStepRegistry()
    {
        var registration = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(TestStepResponse),
            typeof(TestStepHandler),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata("Test-Step", "Test step", "1.0.0", "Test Step"));

        var registry = new Mock<IProcessStepRegistry>();
        registry.Setup(x => x.Registrations).Returns([registration]);
        registry.Setup(x => x.InitialRegistrations).Returns([registration]);
        return registry.Object;
    }

    private static IProcessRegistry CreateProcessRegistry()
    {
        var registry = new Mock<IProcessRegistry>();
        registry.Setup(x => x.Registrations).Returns(
        [
            new ProcessorRegistryItem
            {
                InitialSteps =
                [
                    new ProcessorStepSummary
                    {
                        Name = "Test-Step",
                        Description = "Test step",
                        Version = "1.0.0",
                        DisplayName = "Test Step",
                        Repeatable = false
                    }
                ],
                Steps =
                [
                    new ProcessorStepRegistryItem
                    {
                        Name = "Test-Step",
                        Description = "Test step",
                        Version = "1.0.0",
                        DisplayName = "Test Step",
                        Repeatable = false
                    }
                ]
            }
        ]);
        return registry.Object;
    }

    // ── Queryable registry helpers ───────────────────────────────────────────

    private static IQueryableRegistry CreateQueryableRegistry()
    {
        var registry = new Mock<IQueryableRegistry>();
        registry.Setup(x => x.Registrations).Returns(
        [
            new QueryableContextRegistryItem
            {
                ContextType = typeof(TestContext),
                Name = "Test-Context",
                Description = "Test Context",
                DisplayName = "Test Context",
                Version = "1.0.0",
                Source = "Unit Test",
                Kind = QueryContextKind.Direct,
                Views =
                [
                    new QueryableViewRegistryItem
                    {
                        QueryViewType = typeof(TestView),
                        ViewType = typeof(TestViewContract),
                        ViewParametersType = typeof(EmptyQueryViewParameters),
                        Name = "Test-View",
                        Description = "Test View",
                        DisplayName = "Test View",
                        Version = "1.0.0"
                    }
                ]
            }
        ]);
        return registry.Object;
    }

    // ── Nested test types ────────────────────────────────────────────────────

    public sealed record TestStep;
    public sealed record TestStepResponse;

    public sealed class TestStepHandler : IProcessStepHandler<TestStep, TestStepResponse>
    {
        public Task<ProcessStepHandlerResult<TestStepResponse>> ExecuteAsync(
            TestStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    public sealed class TestContext { }
    public sealed class TestView { }
    public sealed class TestViewContract { }
    public sealed class TestSource { }
}
