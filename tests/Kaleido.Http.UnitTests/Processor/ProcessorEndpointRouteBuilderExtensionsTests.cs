using System.Text;
using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Registry;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessorEndpointRouteBuilderExtensionsTests
    : SutFixture
{
    private static readonly Guid ProcessId = Guid.Parse("0b6f8a52-3c4d-4e1f-9a7b-5d2c8e1f6a30");

    private readonly Mock<IProcessExecutionService> execution = new();
    private readonly Mock<IProcessStateService> state = new();

    [Fact]
    public void MapProcessor_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ProcessorEndpointRouteBuilderExtensions.MapProcessor(null!));
    }

    [Fact]
    public void MapProcessor_WhenTheProcessRuntimeIsNotRegistered_ThrowsAConfigurationError()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = "test-processor" });
        var endpoints = builder.Build();

        var exception = Assert.Throws<KaleidoConfigurationException>(endpoints.MapProcessor);

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, exception.Code);
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

    [Fact]
    public void MapProcessor_LogsTheMappedEndpointsOnceAtInformation()
    {
        var logger = new Mock<ILogger>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var endpoints = CreateEndpoints(serviceName: "workflows", startupLogger: logger);

        endpoints.MapProcessor();

        var log = Assert.Single(logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, log.Arguments[0]);
        Assert.Equal(
            "Process endpoints mapped at route prefix /workflows/processes with 1 process steps and 1 initial steps.",
            log.Arguments[2]?.ToString());
    }

    [Fact]
    public void MapProcessor_ZeroTrust_MapsOnlyStepsWithAnExplicitRule()
    {
        var endpoints = CreateEndpoints(
            mode: KaleidoAuthorizationMode.ZeroTrust,
            registrations:
            [
                CreateRegistration(typeof(TestStep), typeof(TestResponse), "Test-Step", new AuthorizationMetadata(null, ["clerk"])),
                CreateRegistration(typeof(UntypedStep), null, "Untyped-Step", AuthorizationMetadata.Unspecified),
            ]);

        endpoints.MapProcessor();

        Assert.NotNull(FindEndpoint(endpoints, ProcessEndpointNames.StepExecutionEndpointName("test-step")));
        Assert.Null(FindEndpoint(endpoints, ProcessEndpointNames.StepExecutionEndpointName("untyped-step")));
    }

    [Fact]
    public void MapProcessor_AStepWithoutAResultType_IsMappedAsAnUntypedStep()
    {
        var endpoints = CreateEndpoints(
            registrations: [CreateRegistration(typeof(UntypedStep), null, "Untyped-Step", AuthorizationMetadata.Unspecified)]);

        endpoints.MapProcessor();

        var endpoint = FindEndpoint(endpoints, ProcessEndpointNames.StepExecutionEndpointName("untyped-step"));
        Assert.NotNull(endpoint);
        Assert.Single(FindEndpointsByRoute(endpoints, "/test-processor/processes/steps/untyped-step"));
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
        Assert.Contains(produces, x => x.Type == typeof(StepExecutionResponse));
        Assert.Contains("Untyped Step", endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
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

    [Fact]
    public async Task ExecuteEndpoint_PassesTheRequestToTheExecutionService_AndAnswersOk()
    {
        execution
            .Setup(x => x.ExecuteAsync(It.IsAny<ExecuteProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResponse { ProcessId = ProcessId });
        var endpoints = CreateEndpoints();
        endpoints.MapProcessor();

        var context = await InvokeAsync(
            endpoints,
            ProcessEndpointNames.ExecuteEndpointName,
            body: """{"steps":[{"stepName":"Test-Step","request":{}}]}""");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(ProcessId, ReadJson(context).GetProperty("processId").GetGuid());
        execution.Verify(
            x => x.ExecuteAsync(
                It.Is<ExecuteProcessRequest>(r => r.Steps.Single().StepName == "Test-Step"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StateEndpoint_WhenTheProcessExists_AnswersOkWithTheState()
    {
        state
            .Setup(x => x.GetCurrentState(ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessStateResponse { ProcessId = ProcessId });
        var endpoints = CreateEndpoints();
        endpoints.MapProcessor();

        var context = await InvokeAsync(
            endpoints,
            ProcessEndpointNames.ProcessEndpointName,
            routeValues: new() { ["processId"] = ProcessId.ToString() });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(ProcessId, ReadJson(context).GetProperty("processId").GetGuid());
    }

    [Fact]
    public async Task StateEndpoint_WhenTheProcessDoesNotExist_AnswersNotFound()
    {
        state
            .Setup(x => x.GetCurrentState(ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessStateResponse?)null);
        var endpoints = CreateEndpoints();
        endpoints.MapProcessor();

        var context = await InvokeAsync(
            endpoints,
            ProcessEndpointNames.ProcessEndpointName,
            routeValues: new() { ["processId"] = ProcessId.ToString() });

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        state.Verify(x => x.GetCurrentState(ProcessId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TypedStepEndpoint_ExecutesTheTypedStep_AndAnswersOk()
    {
        execution
            .Setup(x => x.ExecuteAsync<TestStep, TestResponse>(It.IsAny<ExecuteStepRequest<TestStep>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepExecutionResponse<TestResponse> { ProcessId = ProcessId, StepName = "Test-Step", Result = new TestResponse() });
        var endpoints = CreateEndpoints();
        endpoints.MapProcessor();

        var context = await InvokeAsync(
            endpoints,
            ProcessEndpointNames.StepExecutionEndpointName("test-step"),
            body: """{"processStep":{}}""");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var json = ReadJson(context);
        Assert.Equal(ProcessId, json.GetProperty("processId").GetGuid());
        Assert.Equal("Test-Step", json.GetProperty("stepName").GetString());
        execution.Verify(
            x => x.ExecuteAsync<TestStep, TestResponse>(It.Is<ExecuteStepRequest<TestStep>>(r => r.ProcessStep != null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UntypedStepEndpoint_ExecutesTheUntypedStep_AndAnswersOk()
    {
        execution
            .Setup(x => x.ExecuteAsync<UntypedStep>(It.IsAny<ExecuteStepRequest<UntypedStep>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepExecutionResponse { ProcessId = ProcessId, StepName = "Untyped-Step" });
        var endpoints = CreateEndpoints(
            registrations: [CreateRegistration(typeof(UntypedStep), null, "Untyped-Step", AuthorizationMetadata.Unspecified)]);
        endpoints.MapProcessor();

        var context = await InvokeAsync(
            endpoints,
            ProcessEndpointNames.StepExecutionEndpointName("untyped-step"),
            body: """{"processStep":{}}""");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("Untyped-Step", ReadJson(context).GetProperty("stepName").GetString());
        execution.Verify(
            x => x.ExecuteAsync<UntypedStep>(It.IsAny<ExecuteStepRequest<UntypedStep>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UntypedStepEndpoint_WhenTheBodyIsNotJson_AnswersUnsupportedMediaType()
    {
        var handler = new Mock<IKaleidoExceptionHandler>();
        handler
            .Setup(x => x.HandleAsync(It.IsAny<HttpContext>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .Callback((HttpContext http, Exception exception, CancellationToken _) =>
                http.Response.StatusCode = ((BadHttpRequestException)exception).StatusCode)
            .Returns(Task.CompletedTask);
        var endpoints = CreateEndpoints(
            registrations: [CreateRegistration(typeof(UntypedStep), null, "Untyped-Step", AuthorizationMetadata.Unspecified)],
            exceptionHandler: handler.Object);
        endpoints.MapProcessor();

        var context = await InvokeAsync(
            endpoints,
            ProcessEndpointNames.StepExecutionEndpointName("untyped-step"),
            body: "plain",
            contentType: "text/plain");

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);
        execution.Verify(
            x => x.ExecuteAsync<UntypedStep>(It.IsAny<ExecuteStepRequest<UntypedStep>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication endpoints,
        string endpointName,
        string? body = null,
        string contentType = "application/json",
        RouteValueDictionary? routeValues = null)
    {
        var endpoint = FindEndpoint(endpoints, endpointName)!;
        var scope = endpoints.Services.CreateScope();
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        context.Response.Body = new MemoryStream();
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(body is not null));

        if (body is not null)
        {
            context.Request.Method = HttpMethods.Post;
            context.Request.ContentType = contentType;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        }

        if (routeValues is not null)
        {
            context.Request.RouteValues = routeValues;
        }

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static JsonElement ReadJson(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body).RootElement.Clone();
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

    private static RouteEndpoint[] FindEndpointsByRoute(
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

    private WebApplication CreateEndpoints(
        string serviceName = "test-processor",
        KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.None,
        AuthorizationMetadata? stepAuthorization = null,
        IReadOnlyCollection<ProcessStepRegistration>? registrations = null,
        Mock<ILogger>? startupLogger = null,
        IKaleidoExceptionHandler? exceptionHandler = null)
    {
        var builder =
            WebApplication.CreateBuilder();

        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());
        builder.Services.AddSingleton<IProcessExecutionService>(execution.Object);
        builder.Services.AddSingleton<IProcessStateService>(state.Object);
        builder.Services.AddSingleton<IProcessorStepRegistry>(CreateRegistry(
            registrations ?? [CreateRegistration(typeof(TestStep), typeof(TestResponse), "Test-Step", stepAuthorization ?? AuthorizationMetadata.Unspecified)]));
        builder.Services.AddSingleton<IProcessorRegistry>(CreateProcessorRegistry());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName, DisplayName = "Test Processor", AuthorizationMode = mode });
        builder.Services.AddSingleton(Mock.Of<IKaleidoIdempotencyStore>());
        builder.Services.AddSingleton(Mock.Of<IKaleidoCorrelationContextAccessor>(x => x.Current == new KaleidoCorrelationContext()));

        if (exceptionHandler is not null)
        {
            builder.Services.AddSingleton(exceptionHandler);
        }

        if (startupLogger is not null)
        {
            var factory = new Mock<ILoggerFactory>();
            factory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(startupLogger.Object);
            builder.Services.AddSingleton(factory.Object);
        }

        return builder.Build();
    }

    private static ProcessStepRegistration CreateRegistration(
        Type stepType,
        Type? resultType,
        string name,
        AuthorizationMetadata authorization) =>
        new(
            stepType,
            resultType,
            typeof(TestStepHandler),
            [],
            [],
            [],
            new RepeatableOptions
            {
                Enabled = false
            },
            new ProcessStepMetadata { Name = name, Description = name, Version = "1.0.0", DisplayName = name.Replace('-', ' '), Authorization = authorization });

    private static IProcessorStepRegistry CreateRegistry(
        IReadOnlyCollection<ProcessStepRegistration> registrations)
    {
        var registry =
            new Mock<IProcessorStepRegistry>();

        registry
            .Setup(x => x.Registrations)
            .Returns(registrations);

        registry
            .Setup(x => x.InitialRegistrations)
            .Returns(registrations);

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

    public sealed record UntypedStep : IProcessStep;

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

    private sealed class BodyDetectionFeature(bool canHaveBody)
        : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => canHaveBody;
    }
}
