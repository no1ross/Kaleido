using Kaleido.Http.Processor;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessorEndpointRouteBuilderExtensionsTests
    : SutFixture
{
    [Fact]
    public void MapProcessor_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ProcessorEndpointRouteBuilderExtensions.MapProcessor(null!));
    }

    [Fact]
    public void MapProcessor_ReturnsRouteGroupBuilder_SoConventionsCompose()
    {
        var endpoints =
            CreateEndpoints();

        var group =
            endpoints.MapProcessor();

        Assert.IsType<RouteGroupBuilder>(group);
    }

    [Fact]
    public void MapProcessor_RegistersStateAndStepEndpoints()
    {
        var endpoints =
            CreateEndpoints();

        endpoints.MapProcessor();

        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ProcessEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.StepExecutionEndpointName("test-step")));
    }

    [Fact]
    public void MapProcessor_UsesExpectedRoutes()
    {
        var endpoints =
            CreateEndpoints(serviceName: "workflows");

        endpoints.MapProcessor();

        Assert.NotEmpty(FindEndpointsByRoute(endpoints, "/workflows/processes/execute"));
        Assert.NotEmpty(FindEndpointsByRoute(endpoints, "/workflows/processes/{processId}"));
        Assert.Single(FindEndpointsByRoute(endpoints, "/workflows/processes/steps/test-step"));
    }

    [Fact]
    public void MapProcessor_UsesDisplayNameTags()
    {
        var endpoints =
            CreateEndpoints();

        endpoints.MapProcessor();

        var executionEndpoint =
            FindEndpoint(
                endpoints,
                ProcessEndpointNames.StepExecutionEndpointName("test-step"))!;

        var executionTags =
            executionEndpoint.Metadata.GetMetadata<ITagsMetadata>();

        Assert.Contains("Test Step", executionTags!.Tags);
    }

    [Theory]
    [InlineData(KaleidoAuthorizationMode.Authenticated)]
    [InlineData(KaleidoAuthorizationMode.ZeroTrust)]
    public void MapProcessor_WhenEnforcing_AndNoStepAllowsAnonymous_ExecuteAndStateRequireACaller(
        KaleidoAuthorizationMode mode)
    {
        var endpoints =
            CreateEndpoints(
                mode: mode,
                stepAuthorization: new AuthorizationMetadata(null, ["clerk"]));

        endpoints.MapProcessor();

        Assert.NotEmpty(AuthorizeData(endpoints, ProcessEndpointNames.ExecuteEndpointName));
        Assert.NotEmpty(AuthorizeData(endpoints, ProcessEndpointNames.ProcessEndpointName));
    }

    [Fact]
    public void MapProcessor_WhenAStepAllowsAnonymous_ExecuteAndStateStayOpen()
    {
        var endpoints =
            CreateEndpoints(
                mode: KaleidoAuthorizationMode.ZeroTrust,
                stepAuthorization: AuthorizationMetadata.Unspecified with { AllowAnonymous = true });

        endpoints.MapProcessor();

        Assert.Empty(AuthorizeData(endpoints, ProcessEndpointNames.ExecuteEndpointName));
        Assert.Empty(AuthorizeData(endpoints, ProcessEndpointNames.ProcessEndpointName));
    }

    [Fact]
    public void MapProcessor_WhenNotEnforcing_ExecuteAndStateAttachNoAuthorization()
    {
        var endpoints =
            CreateEndpoints(
                mode: KaleidoAuthorizationMode.None);

        endpoints.MapProcessor();

        Assert.Empty(AuthorizeData(endpoints, ProcessEndpointNames.ExecuteEndpointName));
        Assert.Empty(AuthorizeData(endpoints, ProcessEndpointNames.ProcessEndpointName));
    }

    private static IReadOnlyCollection<Microsoft.AspNetCore.Authorization.IAuthorizeData> AuthorizeData(
        IEndpointRouteBuilder endpoints,
        string name) =>
        FindEndpoint(endpoints, name)!
            .Metadata
            .GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>();

    private static RouteEndpoint? FindEndpoint(
        IEndpointRouteBuilder endpoints,
        string name) =>
        endpoints.DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .SingleOrDefault(x =>
                x.Metadata
                    .OfType<IEndpointNameMetadata>()
                    .Any(m => string.Equals(
                        m.EndpointName,
                        name,
                        StringComparison.Ordinal)));

    private static IReadOnlyCollection<RouteEndpoint> FindEndpointsByRoute(
        IEndpointRouteBuilder endpoints,
        string route) =>
        endpoints.DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(x => string.Equals(
                Normalize(x.RoutePattern.RawText),
                Normalize(route),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static string Normalize(
        string? route) =>
        (route ?? string.Empty)
            .Trim()
            .Trim('/');

    private static WebApplication CreateEndpoints(
        string serviceName = "test-processor",
        KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.None,
        AuthorizationMetadata? stepAuthorization = null)
    {
        var builder =
            WebApplication.CreateBuilder();

        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());
        builder.Services.AddSingleton<IProcessExecutionService>(Mock.Of<IProcessExecutionService>());
        builder.Services.AddSingleton<IProcessStateService>(Mock.Of<IProcessStateService>());
        builder.Services.AddSingleton<IProcessorStepRegistry>(CreateRegistry(stepAuthorization ?? AuthorizationMetadata.Unspecified));
        builder.Services.AddSingleton<IProcessorRegistry>(CreateProcessorRegistry());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName, DisplayName = "Test Processor", AuthorizationMode = mode });

        return builder.Build();
    }

    private static IProcessorStepRegistry CreateRegistry(
        AuthorizationMetadata authorization)
    {
        var registration =
            new ProcessStepRegistration(
                typeof(TestStep),
                typeof(TestResponse),
                typeof(TestStepHandler),
                [],
                [],
                [],
                new RepeatableOptions
                {
                    Enabled = false
                },
                new ProcessStepMetadata(
                    "Test-Step",
                    "Test step",
                    "1.0.0",
                    "Test Step",
                    authorization));

        var registry =
            new Mock<IProcessorStepRegistry>();

        registry
            .Setup(x => x.Registrations)
            .Returns([registration]);

        registry
            .Setup(x => x.InitialRegistrations)
            .Returns([registration]);

        return registry.Object;
    }

    private static IProcessorRegistry CreateProcessorRegistry()
    {
        var registry =
            new Mock<IProcessorRegistry>();

        registry
            .Setup(x => x.Registrations)
            .Returns(
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

    public sealed record TestStep : IProcessStep;

    public sealed record TestResponse;

    public sealed class TestStepHandler : IProcessStepHandler<TestStep, TestResponse>
    {
        public Task<ProcessStepHandlerResult<TestResponse>> ExecuteAsync(
            TestStep step,
            ProcessStepContext context,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
