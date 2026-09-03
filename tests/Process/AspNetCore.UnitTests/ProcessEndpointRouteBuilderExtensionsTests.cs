using Kaleido.Process.AspNetCore;
using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Execution;
using Kaleido.Process;
using Kaleido.Process.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.AspNetCore.Tests;

public sealed class ProcessEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public void MapProcessor_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ProcessEndpointRouteBuilderExtensions.MapProcessor(null!));
    }

    [Fact]
    public void MapProcessor_RegistersCatalogRegistryStateAndStepEndpoints()
    {
        var endpoints =
            CreateEndpoints();

        endpoints.MapProcessor();

        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ProcessorCatalogEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ExecuteEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.ProcessEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.StepCatalogEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.StepRegistryEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.StepMetadataEndpointName("test-step")));
        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.StepExecutionEndpointName("test-step")));
    }

    [Fact]
    public void MapProcessor_UsesExpectedRoutes()
    {
        var endpoints =
            CreateEndpoints(
                new ProcessRouteOptions
                {
                    RoutePrefix = "/workflows"
                });

        endpoints.MapProcessor();

        Assert.NotEmpty(FindEndpointsByRoute(endpoints, "/workflows/processes"));
        Assert.NotEmpty(FindEndpointsByRoute(endpoints, "/workflows/processes/execute"));
        Assert.NotEmpty(FindEndpointsByRoute(endpoints, "/workflows/processes/{processId}"));
        Assert.NotEmpty(FindEndpointsByRoute(endpoints, "/workflows/processes/steps"));
        Assert.NotEmpty(FindEndpointsByRoute(endpoints, "/workflows/processes/registry"));
        Assert.Equal(2, FindEndpointsByRoute(endpoints, "/workflows/processes/steps/test-step").Count);
    }

    [Fact]
    public void MapProcessor_UsesDisplayNameTags()
    {
        var endpoints =
            CreateEndpoints();

        endpoints.MapProcessor();

        var metadataEndpoint =
            FindEndpoint(
                endpoints,
                ProcessEndpointNames.StepMetadataEndpointName("test-step"))!;

        var executionEndpoint =
            FindEndpoint(
                endpoints,
                ProcessEndpointNames.StepExecutionEndpointName("test-step"))!;

        var metadataTags =
            metadataEndpoint.Metadata.GetMetadata<ITagsMetadata>();

        var executionTags =
            executionEndpoint.Metadata.GetMetadata<ITagsMetadata>();

        Assert.Contains("Test Step", metadataTags!.Tags);
        Assert.Contains("Test Step", executionTags!.Tags);
    }

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
        ProcessRouteOptions? options = null)
    {
        var builder =
            WebApplication.CreateBuilder();

        builder.Services.AddRouting();
        builder.Services.AddSingleton<IProcessExecutionService>(Mock.Of<IProcessExecutionService>());
        builder.Services.AddSingleton<IProcessStateService>(Mock.Of<IProcessStateService>());
        builder.Services.AddSingleton<IProcessStepRegistry>(CreateRegistry());
        builder.Services.AddSingleton<IProcessorRegistry>(CreateProcessorRegistry());
        builder.Services.AddSingleton(options ?? new ProcessRouteOptions());

        return builder.Build();
    }

    private static IProcessStepRegistry CreateRegistry()
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
                    "Test Step"));

        var registry =
            new Mock<IProcessStepRegistry>();

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
                    Name = "test-processor",
                    Description = "Test processor",
                    Version = "1.0.0",
                    DisplayName = "Test Processor",
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

    public sealed record TestStep;

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
