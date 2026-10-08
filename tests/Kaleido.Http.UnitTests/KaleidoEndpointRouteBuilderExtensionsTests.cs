using Kaleido.Exceptions;
using Kaleido.Http.Queryable;
using Kaleido.Http.Registry.Contracts;
using Kaleido.Queryable.Registry;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        Assert.Null(FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context")));
    }

    [Fact]
    public void MapKaleidoHttp_WithQueryableOnly_MapsQueryableEndpoints()
    {
        var endpoints = CreateQueryableOnlyEndpoints();

        endpoints.MapKaleidoHttp();

        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context")));
        Assert.NotNull(FindEndpoint(endpoints, RegistryEndpointNames.RegistryEndpointName));
        Assert.Null(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
    }

    [Fact]
    public void MapKaleidoHttp_WithBoth_MapsBothEndpoints()
    {
        var endpoints = CreateProcessAndQueryableEndpoints();

        endpoints.MapKaleidoHttp();

        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context")));
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

    [Fact]
    public void MapKaleidoHttp_ZeroTrust_UndeclaredStep_IsNotMapped()
    {
        var endpoints = CreateProcessOnlyEndpoints(mode: KaleidoAuthorizationMode.ZeroTrust);

        endpoints.MapKaleidoHttp();

        Assert.Null(FindEndpoint(endpoints, ProcessEndpointNames.StepExecutionEndpointName("test-step")));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
    }

    [Fact]
    public void MapKaleidoHttp_ZeroTrust_ExplicitStep_IsMapped()
    {
        var endpoints = CreateProcessOnlyEndpoints(
            mode: KaleidoAuthorizationMode.ZeroTrust,
            authorization: new AuthorizationMetadata(null, ["radiology"]));

        endpoints.MapKaleidoHttp();

        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.StepExecutionEndpointName("test-step")));
    }

    [Fact]
    public void MapKaleidoHttp_ZeroTrust_UndeclaredSourceAndView_AreNotMapped()
    {
        var endpoints = CreateQueryableOnlyEndpoints(mode: KaleidoAuthorizationMode.ZeroTrust);

        endpoints.MapKaleidoHttp();

        Assert.Null(FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context")));
        Assert.Null(FindEndpoint(endpoints, QueryableEndpointNames.QueryViewEndpointName("test-context", "test-view")));
    }

    [Fact]
    public void MapKaleidoHttp_ZeroTrust_ExplicitViewOnUnspecifiedSource_MapsOnlyView()
    {
        var endpoints = CreateQueryableOnlyEndpoints(
            mode: KaleidoAuthorizationMode.ZeroTrust,
            viewAuthorization: new AuthorizationMetadata(null, ["radiology"]));

        endpoints.MapKaleidoHttp();

        Assert.Null(FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context")));
        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QueryViewEndpointName("test-context", "test-view")));
    }

    [Fact]
    public void MapKaleidoHttp_Authenticated_UndeclaredStep_MapsEndpoints()
    {
        var endpoints = CreateProcessOnlyEndpoints(mode: KaleidoAuthorizationMode.Authenticated);

        endpoints.MapKaleidoHttp();

        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
    }

    [Fact]
    public void MapKaleidoHttp_None_WithDeclaredCapabilities_LogsOneWarning()
    {
        var logger = new Mock<ILogger>();
        var endpoints = CreateProcessOnlyEndpoints(
            authorization: new AuthorizationMetadata(null, ["radiology"]),
            startupLogger: logger);

        endpoints.MapKaleidoHttp();

        logger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MapKaleidoHttp_Authenticated_WithoutHttpAuthorizer_RegistryFailsClosed()
    {
        var endpoints = CreateProcessOnlyEndpoints(mode: KaleidoAuthorizationMode.Authenticated);
        endpoints.MapKaleidoHttp();

        var registry = FindEndpoint(endpoints, RegistryEndpointNames.RegistryEndpointName);
        Assert.NotNull(registry?.RequestDelegate);

        var context = new DefaultHttpContext
        {
            RequestServices = endpoints.Services
        };
        context.Response.Body = new MemoryStream();

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(
            () => registry.RequestDelegate(context));
        Assert.Equal(ConfigurationErrorCodes.AuthenticationNotConfigured, exception.Code);
    }

    [Fact]
    public void MapKaleidoHttp_ZeroTrust_WithoutAuthenticationScheme_ThrowsBeforeMapping()
    {
        var endpoints = CreateEmptyEndpoints(mode: KaleidoAuthorizationMode.ZeroTrust, registerScheme: false);

        var exception = Assert.Throws<KaleidoConfigurationException>(() => endpoints.MapKaleidoHttp());

        Assert.Equal(ConfigurationErrorCodes.AuthenticationNotConfigured, exception.Code);
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

    private static WebApplication CreateEmptyEndpoints(
        string serviceName = "test",
        KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.None,
        bool registerScheme = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName, AuthorizationMode = mode });
        if (registerScheme)
        {
            AddTestScheme(builder);
        }

        return builder.Build();
    }

    private static void AddTestScheme(WebApplicationBuilder builder)
    {
        var provider = new Mock<IAuthenticationSchemeProvider>();
        provider.Setup(x => x.GetAllSchemesAsync())
            .ReturnsAsync([new AuthenticationScheme("test", "test", typeof(IAuthenticationHandler))]);
        builder.Services.AddSingleton(provider.Object);
    }

    private static WebApplication CreateProcessOnlyEndpoints(
        string serviceName = "test", KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.None,
        AuthorizationMetadata? authorization = null, Mock<ILogger>? startupLogger = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName, AuthorizationMode = mode });
        AddTestScheme(builder);
        if (startupLogger is not null)
        {
            var factory = new Mock<ILoggerFactory>();
            factory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(startupLogger.Object);
            builder.Services.AddSingleton(factory.Object);
        }
        builder.Services.AddSingleton<IProcessExecutionService>(Mock.Of<IProcessExecutionService>());
        builder.Services.AddSingleton<IProcessStateService>(Mock.Of<IProcessStateService>());
        builder.Services.AddSingleton<IProcessorResponseFactory>(Mock.Of<IProcessorResponseFactory>());
        builder.Services.AddSingleton<IProcessorStepRegistry>(CreateProcessStepRegistry(authorization));
        builder.Services.AddSingleton<IProcessorRegistry>(CreateProcessRegistry());
        return builder.Build();
    }

    private static WebApplication CreateQueryableOnlyEndpoints(
        string serviceName = "test", KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.None,
        AuthorizationMetadata? viewAuthorization = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName, AuthorizationMode = mode });
        AddTestScheme(builder);
        builder.Services.AddSingleton(Mock.Of<IQueryableService>());
        builder.Services.AddSingleton<IQueryableRegistry>(CreateQueryableRegistry(viewAuthorization));
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
        builder.Services.AddSingleton<IProcessorStepRegistry>(CreateProcessStepRegistry());
        builder.Services.AddSingleton<IProcessorRegistry>(CreateProcessRegistry());
        builder.Services.AddSingleton(Mock.Of<IQueryableService>());
        builder.Services.AddSingleton<IQueryableRegistry>(CreateQueryableRegistry());
        return builder.Build();
    }

    // ── Process registry helpers ─────────────────────────────────────────────

    private static IProcessorStepRegistry CreateProcessStepRegistry(AuthorizationMetadata? authorization = null)
    {
        var registration = new ProcessStepRegistration(
            typeof(TestStep),
            typeof(TestStepResponse),
            typeof(TestStepHandler),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata("Test-Step", "Test step", "1.0.0", "Test Step", authorization ?? AuthorizationMetadata.Unspecified));

        var registry = new Mock<IProcessorStepRegistry>();
        registry.Setup(x => x.Registrations).Returns([registration]);
        registry.Setup(x => x.InitialRegistrations).Returns([registration]);
        return registry.Object;
    }

    private static IProcessorRegistry CreateProcessRegistry()
    {
        var registry = new Mock<IProcessorRegistry>();
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

    private static IQueryableRegistry CreateQueryableRegistry(
        AuthorizationMetadata? viewAuthorization = null)
    {
        var registry = new Mock<IQueryableRegistry>();
        registry.Setup(x => x.Registrations).Returns(
        [
            new QueryableSourceRegistryItem
            {
                SourceType = typeof(TestSource),
                QueryContextType = typeof(TestContext),
                ResultType = typeof(TestContext),
                ParametersType = typeof(EmptyQueryViewParameters),
                Name = "Test-Context",
                Description = "Test Context",
                DisplayName = "Test Context",
                Version = "1.0.0",
                Source = "Unit Test",
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
                        Version = "1.0.0",
                        Authorization = viewAuthorization ?? AuthorizationMetadata.Unspecified
                    }
                ]
            }
        ]);
        return registry.Object;
    }

    // ── Nested test types ────────────────────────────────────────────────────

    public sealed record TestStep : IProcessStep;
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
