using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Http.Authorization;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessExecutionServiceTests
    : SutFixture
{
    private ProcessExecutionService CreateSut() =>
        CreateSut(
            Mock.Of<IProcessorStepRegistry>(),
            Mock.Of<IProcessorRuntime>(),
            AnyResponses());

    private static ProcessExecutionService CreateSut(
        IProcessorStepRegistry registry,
        IProcessorRuntime runtime,
        IProcessExecutionResponseFactory responseFactory,
        Guid? contextProcessId = null,
        IKaleidoAuthorizer? authorizer = null,
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<ProcessExecutionService>? logger = null,
        IProcessContinuationFilter? continuations = null)
    {
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation
            .Setup(x => x.Current)
            .Returns(new KaleidoCorrelationContext
            {
                RequestId = Guid.NewGuid().ToString(),
                ProcessId = contextProcessId
            });

        var stepAuthorizer = new Mock<IKaleidoAuthorizer>();
        stepAuthorizer
            .Setup(x => x.AuthorizeAsync(
                It.IsAny<AuthorizationMetadata?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ProcessExecutionService(
            httpContextAccessor ?? new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            registry,
            runtime,
            new KaleidoServiceOptions { ServiceName = "test-processor" },
            correlation.Object,
            responseFactory,
            authorizer ?? stepAuthorizer.Object,
            continuations ?? PassThrough(),
            logger ?? NullLogger<ProcessExecutionService>.Instance);
    }

    // A response for any result: what the tests that do not look at it need.
    private static IProcessExecutionResponseFactory AnyResponses()
    {
        var factory = new Mock<IProcessExecutionResponseFactory>();
        factory
            .Setup(x => x.CreateExecutionResponse(It.IsAny<ProcessResult>(), It.IsAny<IProcessorStepRegistry>(), It.IsAny<string>()))
            .Returns((ProcessResult result, IProcessorStepRegistry _, string _) => new ProcessExecutionResponse { ProcessId = result.ProcessId });
        factory
            .Setup(x => x.CreateStepResponse(It.IsAny<ProcessResult>(), It.IsAny<ProcessStepResult>(), It.IsAny<IProcessorStepRegistry>(), It.IsAny<string>()))
            .Returns((ProcessResult result, ProcessStepResult step, IProcessorStepRegistry _, string _) => new StepExecutionResponse { ProcessId = result.ProcessId, StepName = step.StepName });
        factory
            .Setup(x => x.CreateStepResponse<TestResponse>(It.IsAny<ProcessResult>(), It.IsAny<ProcessStepResult>(), It.IsAny<IProcessorStepRegistry>(), It.IsAny<string>()))
            .Returns((ProcessResult result, ProcessStepResult step, IProcessorStepRegistry _, string _) => new StepExecutionResponse<TestResponse> { ProcessId = result.ProcessId, StepName = step.StepName });
        return factory.Object;
    }

    // #524: what the caller is described is the continuation filter's decision, for the execute and
    // the per-step responses alike.
    [Fact]
    public async Task TheResponses_DescribeTheCallerOnlyWhatTheFilterPermits()
    {
        var registration = CreateRegistration();
        var runtime = new Mock<IProcessorRuntime>();
        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(registration.Metadata.Name, new TestResponse()));
        var sut = CreateSut(CreateRegistry(registration), runtime.Object, AnyResponses(), continuations: NothingPermitted());

        var execute = await sut.ExecuteAsync(
            new ExecuteProcessRequest { Steps = [new ProcessStepRequest { StepName = registration.Metadata.Name, Request = JsonSerializer.SerializeToElement(new { }) }] },
            CancellationToken.None);
        var step = await sut.ExecuteAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.Equal(ProcessContinuationFilter.NotPermitted, execute.Notice);
        Assert.Equal(ProcessContinuationFilter.NotPermitted, step.Notice);
        Assert.Null(step.RequiredStep);
        Assert.Empty(step.AvailableSteps);
    }

    // The continuation filter (its own fixture) leaves the response as it is.
    private static IProcessContinuationFilter PassThrough()
    {
        var filter = new Mock<IProcessContinuationFilter>();
        filter
            .Setup(x => x.FilterAsync(It.IsAny<Guid>(), It.IsAny<ProcessRequiredStep?>(), It.IsAny<IReadOnlyCollection<ProcessStepSummary>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, ProcessRequiredStep? required, IReadOnlyCollection<ProcessStepSummary> available, CancellationToken _) => new ProcessContinuation(required, available, Notice: null));
        return filter.Object;
    }

    // The filter decides what the caller is described (#524): the service answers with its result.
    private static IProcessContinuationFilter NothingPermitted()
    {
        var filter = new Mock<IProcessContinuationFilter>();
        filter
            .Setup(x => x.FilterAsync(It.IsAny<Guid>(), It.IsAny<ProcessRequiredStep?>(), It.IsAny<IReadOnlyCollection<ProcessStepSummary>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessContinuation(RequiredStep: null, AvailableSteps: [], ProcessContinuationFilter.NotPermitted));
        return filter.Object;
    }

    [Fact]
    public async Task ExecuteAsync_ProcessRequest_EchoesTheProcessIdHeader_AndLogsAtDebug()
    {
        var registration = CreateRegistration();
        var processResult = CreateProcessResult(registration.Metadata.Name, new TestResponse());
        var runtime = new Mock<IProcessorRuntime>();
        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);
        var httpContext = new DefaultHttpContext();
        var logger = CreateLogger();

        await CreateSut(
                CreateRegistry(registration),
                runtime.Object,
                AnyResponses(),
                httpContextAccessor: new HttpContextAccessor { HttpContext = httpContext },
                logger: logger.Object)
            .ExecuteAsync(
                new ExecuteProcessRequest
                {
                    Steps = [new ProcessStepRequest { StepName = registration.Metadata.Name, Request = JsonSerializer.SerializeToElement(new { }) }]
                },
                CancellationToken.None);

        Assert.Equal(processResult.ProcessId.ToString(), httpContext.Response.Headers[KaleidoCorrelationHeaders.ProcessId].ToString());
        Assert.Equal(["Executing process for processor test-processor with 1 submitted step(s)."], DebugLogs(logger));
    }

    [Fact]
    public async Task ExecuteAsync_Step_EchoesTheProcessIdHeader_AndLogsAtDebug()
    {
        var registration = CreateRegistration();
        var processResult = CreateProcessResult(registration.Metadata.Name, new TestResponse());
        var runtime = new Mock<IProcessorRuntime>();
        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);
        var httpContext = new DefaultHttpContext();
        var logger = CreateLogger();

        await CreateSut(
                CreateRegistry(registration),
                runtime.Object,
                AnyResponses(),
                httpContextAccessor: new HttpContextAccessor { HttpContext = httpContext },
                logger: logger.Object)
            .ExecuteAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.Equal(processResult.ProcessId.ToString(), httpContext.Response.Headers[KaleidoCorrelationHeaders.ProcessId].ToString());
        Assert.Equal(["Executing step Test-Step for processor test-processor."], DebugLogs(logger));
    }

    [Fact]
    public async Task ExecuteAsync_OutsideAnHttpRequest_ReturnsTheResponseWithoutHeaders()
    {
        var registration = CreateRegistration();
        var registry = CreateRegistry(registration);
        var processResult = CreateProcessResult(registration.Metadata.Name, new TestResponse());
        var runtime = new Mock<IProcessorRuntime>();
        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);
        var expected = new ProcessExecutionResponse { ProcessId = processResult.ProcessId };
        var responseFactory = new Mock<IProcessExecutionResponseFactory>();
        responseFactory
            .Setup(x => x.CreateExecutionResponse(processResult, registry, "test-processor"))
            .Returns(expected);

        var response = await CreateSut(
                registry,
                runtime.Object,
                responseFactory.Object,
                httpContextAccessor: new HttpContextAccessor())
            .ExecuteAsync(new ExecuteProcessRequest { Steps = [] }, CancellationToken.None);

        Assert.Equal(expected, response);
    }

    [Fact]
    public async Task ExecuteAsync_Step_WhenTheResultHasNoEntryForTheStep_ThrowsAFrameworkError()
    {
        var registration = CreateRegistration();
        var runtime = new Mock<IProcessorRuntime>();
        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult("Other-Step", new TestResponse()));

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            CreateSut(CreateRegistry(registration), runtime.Object, AnyResponses())
                .ExecuteAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None));

        Assert.Equal(ProcessorErrorCodes.MissingStepResult, exception.Code);
        Assert.Equal("Process result for step 'Test-Step' contained no entry for that step.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Step_WhenTheStepAppearsMoreThanOnce_ReportsTheEntryThatRan()
    {
        var registration = CreateRegistration();
        var ran = CreateProcessResult(registration.Metadata.Name, new TestResponse()).Steps.Single();
        var pending = ran with { ExecutionStatus = StepExecutionStatus.Pending, Response = null };
        var processResult = CreateProcessResult(registration.Metadata.Name, new TestResponse()) with
        {
            Steps = [pending, ran with { StepName = "test-step" }, pending],
        };
        var runtime = new Mock<IProcessorRuntime>();
        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);
        ProcessStepResult? reported = null;
        var responseFactory = new Mock<IProcessExecutionResponseFactory>();
        responseFactory
            .Setup(x => x.CreateStepResponse(It.IsAny<ProcessResult>(), It.IsAny<ProcessStepResult>(), It.IsAny<IProcessorStepRegistry>(), It.IsAny<string>()))
            .Callback((ProcessResult _, ProcessStepResult step, IProcessorStepRegistry _, string _) => reported = step)
            .Returns(new StepExecutionResponse { ProcessId = processResult.ProcessId, StepName = registration.Metadata.Name });

        await CreateSut(CreateRegistry(registration), runtime.Object, responseFactory.Object)
            .ExecuteAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.NotNull(reported);
        Assert.Equal(StepExecutionStatus.Completed, reported.ExecutionStatus);
        Assert.Equal("test-step", reported.StepName);
    }

    private static Mock<ILogger<ProcessExecutionService>> CreateLogger()
    {
        var logger = new Mock<ILogger<ProcessExecutionService>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return logger;
    }

    private static string?[] DebugLogs(Mock<ILogger<ProcessExecutionService>> logger) =>
        logger.Invocations
            .Where(x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal) && Equals(x.Arguments[0], LogLevel.Debug))
            .Select(x => x.Arguments[2]?.ToString())
            .ToArray();

    [Fact]
    public async Task ExecuteAsync_ProcessRequest_MapsRequestAndReturnsResponse()
    {
        var registration = CreateRegistration();
        var registry = CreateRegistry(registration);

        ProcessRequest? capturedRequest = null;
        var runtime = new Mock<IProcessorRuntime>();
        var processResult = CreateProcessResult(registration.Metadata.Name, new TestResponse());

        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(processResult);

        var contextProcessId = Guid.NewGuid();

        var expectedResponse = new ProcessExecutionResponse
        {
            ProcessId = processResult.ProcessId,
            AvailableSteps = [],
            Results = []
        };

        var responseFactory = new Mock<IProcessExecutionResponseFactory>();
        responseFactory
            .Setup(x => x.CreateExecutionResponse(
                processResult,
                registry,
                "test-processor"))
            .Returns(expectedResponse);

        var service = CreateSut(registry, runtime.Object, responseFactory.Object, contextProcessId);

        var request = new ExecuteProcessRequest
        {
            Steps =
            [
                new ProcessStepRequest
                {
                    StepName = registration.Metadata.Name,
                    Request = JsonSerializer.SerializeToElement(new { value = "abc" })
                }
            ]
        };

        var response = await service.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(contextProcessId, capturedRequest.ProcessId);
        Assert.True(capturedRequest.Processor.Steps.ContainsKey(registration.Metadata.Name));

        Assert.Equal(expectedResponse, response);
        responseFactory.Verify(
            x => x.CreateExecutionResponse(
                processResult,
                registry,
                "test-processor"),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TypedStep_UsesRegistrationNameAndReturnsTypedResponse()
    {
        var registration = CreateRegistration();
        var registry = CreateRegistry(registration);

        ProcessRequest? capturedRequest = null;
        var runtime = new Mock<IProcessorRuntime>();

        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(CreateProcessResult(registration.Metadata.Name, new TestResponse()));

        var expectedResponse = new StepExecutionResponse<TestResponse>
        {
            ProcessId = Guid.NewGuid(),
            StepName = registration.Metadata.Name
        };

        var responseFactory = new Mock<IProcessExecutionResponseFactory>();
        responseFactory
            .Setup(x => x.CreateStepResponse<TestResponse>(
                It.IsAny<ProcessResult>(),
                It.IsAny<ProcessStepResult>(),
                registry,
                "test-processor"))
            .Returns(expectedResponse);

        var service = CreateSut(registry, runtime.Object, responseFactory.Object, Guid.NewGuid());

        var request = new ExecuteStepRequest<TestStep>
        {
            ProcessStep = new TestStep()
        };

        var response = await service.ExecuteAsync<TestStep, TestResponse>(request, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Processor.Steps.ContainsKey(registration.Metadata.Name));
        Assert.Equal(expectedResponse, response);
    }

    [Fact]
    public async Task ExecuteAsync_UntypedStep_UsesRegistrationNameAndReturnsResponse()
    {
        var registration = CreateRegistration();
        var registry = CreateRegistry(registration);

        var runtime = new Mock<IProcessorRuntime>();

        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult(registration.Metadata.Name, new TestResponse()));

        var expectedResponse = new StepExecutionResponse
        {
            ProcessId = Guid.NewGuid(),
            StepName = registration.Metadata.Name
        };

        var responseFactory = new Mock<IProcessExecutionResponseFactory>();
        responseFactory
            .Setup(x => x.CreateStepResponse(
                It.IsAny<ProcessResult>(),
                It.IsAny<ProcessStepResult>(),
                registry,
                "test-processor"))
            .Returns(expectedResponse);

        var service = CreateSut(registry, runtime.Object, responseFactory.Object, Guid.NewGuid());

        var request = new ExecuteStepRequest<TestStep>
        {
            ProcessStep = new TestStep()
        };

        var response = await service.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(expectedResponse, response);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepNotAuthorized_Throws()
    {
        var registration = CreateRegistration();
        var registry = CreateRegistry(registration);
        var runtime = new Mock<IProcessorRuntime>();

        var authorizer = new Mock<IKaleidoAuthorizer>();
        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.IsAny<AuthorizationMetadata?>(),
                registration.Metadata.Name,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new KaleidoAuthorizationException(
                    registration.Metadata.Name,
                    callerIsAuthenticated: true));

        var service = CreateSut(
            registry,
            runtime.Object,
            AnyResponses(),
            authorizer: authorizer.Object);

        var request = new ExecuteProcessRequest
        {
            Steps =
            [
                new ProcessStepRequest
                {
                    StepName = registration.Metadata.Name,
                    Request = JsonSerializer.SerializeToElement(new { value = "abc" })
                }
            ]
        };

        await Assert.ThrowsAsync<KaleidoAuthorizationException>(
            () => service.ExecuteAsync(request, CancellationToken.None));

        runtime.Verify(
            x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAStepIsSubmittedTwice_RejectsTheRequestBeforeRunning()
    {
        var runtime = new Mock<IProcessorRuntime>();
        var service = CreateSut(
            Mock.Of<IProcessorStepRegistry>(),
            runtime.Object,
            AnyResponses());

        var request = new ExecuteProcessRequest
        {
            Steps =
            [
                new ProcessStepRequest { StepName = "CaptureMember", Request = JsonSerializer.SerializeToElement(new { }) },
                new ProcessStepRequest { StepName = "capturemember", Request = JsonSerializer.SerializeToElement(new { }) },
            ]
        };

        var exception = await Assert.ThrowsAsync<KaleidoValidationException>(
            () => service.ExecuteAsync(request, CancellationToken.None));

        Assert.Equal(ProcessorErrorCodes.InvalidRequest, exception.Code);
        Assert.Contains("'capturemember'", exception.Message, StringComparison.Ordinal);
        runtime.Verify(
            x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnySubmittedStepDenied_RejectsWholeRequestBeforeRunning()
    {
        var allowed = CreateRegistration();
        var registry = new Mock<IProcessorStepRegistry>();
        registry.Setup(x => x.Find(allowed.Metadata.Name)).Returns(allowed);

        var runtime = new Mock<IProcessorRuntime>();

        var authorizer = new Mock<IKaleidoAuthorizer>();
        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.IsAny<AuthorizationMetadata?>(),
                allowed.Metadata.Name,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.IsAny<AuthorizationMetadata?>(),
                "Denied",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new KaleidoAuthorizationException(
                    "Denied",
                    callerIsAuthenticated: false));

        var service = CreateSut(
            registry.Object,
            runtime.Object,
            AnyResponses(),
            authorizer: authorizer.Object);

        var request = new ExecuteProcessRequest
        {
            Steps =
            [
                new ProcessStepRequest
                {
                    StepName = allowed.Metadata.Name,
                    Request = JsonSerializer.SerializeToElement(new { value = "abc" })
                },
                new ProcessStepRequest
                {
                    StepName = "Denied",
                    Request = JsonSerializer.SerializeToElement(new { })
                }
            ]
        };

        await Assert.ThrowsAsync<KaleidoAuthorizationException>(
            () => service.ExecuteAsync(request, CancellationToken.None));

        runtime.Verify(
            x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepUnknown_AuthorizesAsUndeclared()
    {
        var registry = new Mock<IProcessorStepRegistry>();
        var authorizer = new Mock<IKaleidoAuthorizer>();

        var service = CreateSut(
            registry.Object,
            CompletingRuntime(),
            AnyResponses(),
            authorizer: authorizer.Object);

        var exception = await Assert.ThrowsAsync<KaleidoValidationException>(() => service.ExecuteAsync(
            new ExecuteProcessRequest
            {
                Steps =
                [
                    new ProcessStepRequest
                    {
                        StepName = "NoSuchStep",
                        Request = JsonSerializer.SerializeToElement(new { })
                    }
                ]
            },
            CancellationToken.None));

        // Authorized as undeclared first (fail closed), then rejected before anything runs (#344).
        authorizer.Verify(
            x => x.AuthorizeAsync(null, "NoSuchStep", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(ProcessorErrorCodes.UnknownStep, exception.Code);
        Assert.Contains("'NoSuchStep'", exception.Message, StringComparison.Ordinal);
    }

    // /execute builds each step in the transport (KaleidoJsonOptions); core receives typed steps (#344).
    [Fact]
    public async Task ExecuteAsync_BuildsEachStepAsItsStepType_BeforeTheRuntime()
    {
        var registry = TypedInputRegistry();
        ProcessRequest? captured = null;
        var runtime = new Mock<IProcessorRuntime>();
        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(CreateProcessResult(nameof(TypedInputStep), new TestResponse()));
        var itemId = Guid.NewGuid();

        await CreateSut(registry, runtime.Object, AnyResponses()).ExecuteAsync(
            new ExecuteProcessRequest { Steps = [new ProcessStepRequest { StepName = "typedinputstep", Request = JsonSerializer.SerializeToElement(new { itemId }) }] },
            TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        var step = Assert.IsType<TypedInputStep>(captured.Processor.Steps["TypedInputStep"]);
        Assert.Equal(itemId, step.ItemId);
    }

    [Theory]
    [InlineData("{\"itemId\":\"not-a-guid\"}", "itemId")]
    [InlineData("{}", "itemId")]
    [InlineData("null", "no input")]
    public async Task ExecuteAsync_AStepThatCannotBeBuilt_IsRejected_NamingTheStep_AndNothingRuns(string input, string named)
    {
        var runtime = new Mock<IProcessorRuntime>();
        using var json = JsonDocument.Parse(input);

        var exception = await Assert.ThrowsAsync<Microsoft.AspNetCore.Http.BadHttpRequestException>(() =>
            CreateSut(TypedInputRegistry(), runtime.Object, AnyResponses()).ExecuteAsync(
                new ExecuteProcessRequest { Steps = [new ProcessStepRequest { StepName = nameof(TypedInputStep), Request = json.RootElement.Clone() }] },
                TestContext.Current.CancellationToken));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("Step 'TypedInputStep'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(named, exception.Message, StringComparison.Ordinal);
        runtime.Verify(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IProcessorStepRegistry TypedInputRegistry()
    {
        var registration = new ProcessStepRegistration(
            typeof(TypedInputStep),
            null,
            typeof(TestStepHandler),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = nameof(TypedInputStep), Description = "Typed input", DisplayName = "Typed input" });
        var registry = new Mock<IProcessorStepRegistry>();
        registry.Setup(x => x.Find(It.Is<string>(n => string.Equals(n, nameof(TypedInputStep), StringComparison.OrdinalIgnoreCase)))).Returns(registration);
        return registry.Object;
    }

    public sealed record TypedInputStep : IProcessStep
    {
        public required Guid ItemId { get; init; }
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoStepsSubmitted_AuthorizesAsUndeclared()
    {
        var authorizer = new Mock<IKaleidoAuthorizer>();

        var service = CreateSut(
            Mock.Of<IProcessorStepRegistry>(),
            CompletingRuntime(),
            AnyResponses(),
            authorizer: authorizer.Object);

        await service.ExecuteAsync(
            new ExecuteProcessRequest { Steps = [] },
            CancellationToken.None);

        authorizer.Verify(
            x => x.AuthorizeAsync(null, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static IProcessorRuntime CompletingRuntime()
    {
        var runtime = new Mock<IProcessorRuntime>();
        runtime
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult("any", new TestResponse()));
        return runtime.Object;
    }

    private static ProcessResult CreateProcessResult(string stepName, object response) =>
        new()
        {
            ProcessId = Guid.NewGuid(),
            State = ProcessExecutionState.Active,
            AvailableSteps = [stepName],
            Steps =
            [
                new ProcessStepResult
                {
                    StepName = stepName,
                    CandidateStatus = StepCandidateStatus.Built,
                    IncludedInExecutionPlan = true,
                    Response = response,
                    ExecutionStatus = StepExecutionStatus.Completed,
                    RuntimeMessages = [],
                    BusinessMessages = []
                }
            ]
        };

    private static IProcessorStepRegistry CreateRegistry(ProcessStepRegistration registration)
    {
        var registry = new Mock<IProcessorStepRegistry>();

        registry.Setup(x => x.GetRegistration(typeof(TestStep))).Returns(registration);
        registry.Setup(x => x.GetRegistration(registration.Metadata.Name)).Returns(registration);
        registry.Setup(x => x.Find(registration.Metadata.Name)).Returns(registration);

        return registry.Object;
    }

    private static ProcessStepRegistration CreateRegistration() =>
        new(
            typeof(TestStep),
            typeof(TestResponse),
            typeof(TestStepHandler),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = "Test-Step", Description = "Test step", DisplayName = "Test Step" });

    public sealed record TestStep : IProcessStep;
    public sealed record TestResponse;

    public sealed class TestStepHandler : IProcessStepHandler<TestStep, TestResponse>
    {
        public Task<ProcessStepHandlerResult<TestResponse>> ExecuteAsync(
            TestStep step,
            ProcessStepContext context,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
