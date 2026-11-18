using System.Text.Json;
using Kaleido.Http;
using Kaleido.Http.Server.Authorization;
using Kaleido.Observability;
using Kaleido.Process.Registry;
using Kaleido.Registry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Process.Http.Server.UnitTests;

public sealed class ProcessExecutionServiceTests : Kaleido.UnitTests.SutFixture
{
    private static readonly KaleidoNotice TargetNotice = new("target_notice", "The target left something out.");
    private readonly Mock<IProcessorRuntime> runtime = new();

    private ProcessExecutionService CreateSut(
        IProcessorStepRegistry? registry = null,
        IProcessorRuntime? processor = null,
        Guid? contextProcessId = null,
        IKaleidoAuthorizer? authorizer = null,
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<ProcessExecutionService>? logger = null,
        IProcessContinuationFilter? continuations = null,
        KaleidoProcessHttpOptions? options = null)
    {
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { RequestId = Guid.NewGuid().ToString(), ProcessId = contextProcessId });
        var authorized = new Mock<IKaleidoAuthorizer>();
        authorized.Setup(x => x.AuthorizeAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new ProcessExecutionService(
            httpContextAccessor ?? new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            registry ?? Mock.Of<IProcessorStepRegistry>(),
            processor ?? runtime.Object,
            new KaleidoServiceOptions { ServiceName = "test-processor", ServiceVersion = "1" },
            correlation.Object,
            continuations ?? PassThrough(),
            authorizer ?? authorized.Object,
            options ?? new KaleidoProcessHttpOptions(),
            logger ?? NullLogger<ProcessExecutionService>.Instance);
    }

    // #524: what the caller is described is the continuation filter's decision, for the execute and
    // the per-step responses alike.
    [Fact]
    public async Task TheResponses_DescribeTheCallerOnlyWhatTheFilterPermits()
    {
        var registration = CreateRegistration();
        var result = CreateProcessResult(registration.Metadata.Name, new TestResponse()) with
        {
            RequiredStep = "NextStep",
            RequiredInformationRequest = new InformationRequest { InformationRequestId = "questions" },
        };
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        var sut = CreateSut(CreateRegistry(registration), continuations: NothingPermitted());

        var execute = await sut.ExecuteAsync(Request(registration.Metadata.Name), CancellationToken.None);
        var step = await sut.ExecuteStepAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.Equal(ProcessContinuationFilter.NotPermitted, execute.Notice);
        Assert.Equal(ProcessContinuationFilter.NotPermitted, step.Notice);
        Assert.Null(execute.RequiredStep);
        Assert.Null(step.RequiredInformationRequest);
        Assert.Empty(step.AvailableSteps);
    }

    // #198: after a hand-off the caller is answered with the target's already authorized next
    // step names and its pinned version. The local adapter does not resolve the target's Registry
    // routes, on either the execute or per-step response. Nothing local is offered.
    [Fact]
    public async Task AfterAHandOff_TheResponsesOfferTheTargetsSteps_CheckedForTheCaller()
    {
        var registration = CreateRegistration();
        var handOff = CreateProcessResult(registration.Metadata.Name, new TestResponse()) with
        {
            TargetProcessorName = "radiology",
            TargetProcessorVersion = "2",
            AvailableSteps = ["ValidateMemberStep"],
        };
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(handOff);
        var filter = new Mock<IProcessContinuationFilter>();
        filter.Setup(x => x.FilterAsync(handOff.ProcessId, null, It.Is<IReadOnlyCollection<string>>(names => names.Single() == "ValidateMemberStep"), "radiology", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessContinuation(null, ["ValidateMemberStep"], null));
        var sut = CreateSut(CreateRegistry(registration), continuations: filter.Object);

        var execute = await sut.ExecuteAsync(Request(registration.Metadata.Name), CancellationToken.None);
        var step = await sut.ExecuteStepAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.Equal(["ValidateMemberStep"], execute.AvailableSteps);
        Assert.Equal(["ValidateMemberStep"], step.AvailableSteps);
        Assert.Equal("2", execute.TargetProcessorVersion);
        Assert.Equal("2", step.TargetProcessorVersion);
        filter.Verify(x => x.FilterAsync(handOff.ProcessId, null, It.IsAny<IReadOnlyCollection<string>>(), "radiology", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // The target's notice reaches the caller; the caller check's own notice takes its place when it
    // leaves steps out.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AfterAHandOff_TheNotice_IsTheTargetsUnlessTheCallerCheckSetsOne(bool callerCheckLeavesStepsOut)
    {
        var registration = CreateRegistration();
        var handOff = CreateProcessResult(registration.Metadata.Name, new TestResponse()) with { TargetProcessorName = "radiology", TargetProcessorVersion = "2", Notice = TargetNotice };
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(handOff);
        var sut = CreateSut(CreateRegistry(registration), continuations: callerCheckLeavesStepsOut ? NothingPermitted() : PassThrough());

        var result = await sut.ExecuteStepAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.Equal(callerCheckLeavesStepsOut ? ProcessContinuationFilter.NotPermitted : TargetNotice, result.Notice);
    }

    // The continuation filter (its own fixture) leaves the response as it is.
    private static IProcessContinuationFilter PassThrough()
    {
        var filter = new Mock<IProcessContinuationFilter>();
        filter.Setup(x => x.FilterAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string? required, IReadOnlyCollection<string> available, string? _, CancellationToken _) =>
                new ProcessContinuation(required, available, Notice: null));
        return filter.Object;
    }

    // The filter decides what the caller is described (#524): the service answers with its result.
    private static IProcessContinuationFilter NothingPermitted()
    {
        var filter = new Mock<IProcessContinuationFilter>();
        filter.Setup(x => x.FilterAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessContinuation(null, [], ProcessContinuationFilter.NotPermitted));
        return filter.Object;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_FrameworkMessagesFollowTheProcessHttpOption_WithoutRemovingBusinessMessages(bool includeFrameworkMessages)
    {
        var registration = CreateRegistration();
        var result = CreateProcessResult(registration.Metadata.Name, new TestResponse());
        var diagnostic = new StepProcessingMessage { Type = MessageType.Warning, Code = "trace", Message = "Framework detail." };
        var business = new ProcessMessage { Type = MessageType.Information, Code = "accepted", Message = "Accepted." };
        result = result with
        {
            Steps = [result.Steps.Single() with { RuntimeMessages = [diagnostic], BusinessMessages = [business] }],
        };
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

        var response = await CreateSut(CreateRegistry(registration), options: new KaleidoProcessHttpOptions
        {
            IncludeFrameworkMessages = includeFrameworkMessages,
        }).ExecuteAsync(Request(registration.Metadata.Name), TestContext.Current.CancellationToken);

        var step = Assert.Single(response.Steps);
        Assert.Equal(includeFrameworkMessages ? [diagnostic] : [], step.RuntimeMessages);
        Assert.Equal([business], step.BusinessMessages);
        Assert.Equal([diagnostic], Assert.Single(result.Steps).RuntimeMessages);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessRequest_EchoesTheProcessIdHeader_AndLogsAtDebug()
    {
        var registration = CreateRegistration();
        var result = CreateProcessResult(registration.Metadata.Name, new TestResponse());
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        var http = new DefaultHttpContext();
        var logger = CreateLogger();

        await CreateSut(CreateRegistry(registration), httpContextAccessor: new HttpContextAccessor { HttpContext = http }, logger: logger.Object)
            .ExecuteAsync(Request(registration.Metadata.Name), CancellationToken.None);

        Assert.Equal(result.ProcessId.ToString(), http.Response.Headers[KaleidoCorrelationHeaders.ProcessId].ToString());
        Assert.Equal(["Executing process for processor test-processor with 1 submitted step(s)."], DebugLogs(logger));
    }

    [Fact]
    public async Task ExecuteAsync_Step_EchoesTheProcessIdHeader_AndLogsAtDebug()
    {
        var registration = CreateRegistration();
        var result = CreateProcessResult(registration.Metadata.Name, new TestResponse());
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        var http = new DefaultHttpContext();
        var logger = CreateLogger();

        await CreateSut(CreateRegistry(registration), httpContextAccessor: new HttpContextAccessor { HttpContext = http }, logger: logger.Object)
            .ExecuteStepAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.Equal(result.ProcessId.ToString(), http.Response.Headers[KaleidoCorrelationHeaders.ProcessId].ToString());
        Assert.Equal(["Executing step Test-Step for processor test-processor."], DebugLogs(logger));
    }

    [Fact]
    public async Task ExecuteAsync_OutsideAnHttpRequest_ReturnsTheResponseWithoutHeaders()
    {
        var result = CreateProcessResult("any", new TestResponse());
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

        var response = await CreateSut(httpContextAccessor: new HttpContextAccessor()).ExecuteAsync(new ExecuteProcessRequest(), CancellationToken.None);

        Assert.Equal(result.ProcessId, response.ProcessId);
        Assert.Equal(result.Steps.Select(x => x.StepName), response.Steps.Select(x => x.StepName));
    }

    [Fact]
    public async Task ExecuteAsync_Step_WhenTheResultHasNoEntryForTheStep_ThrowsAFrameworkError()
    {
        var registration = CreateRegistration();
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult("Other-Step", new TestResponse()));

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() => CreateSut(CreateRegistry(registration))
            .ExecuteStepAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None));

        Assert.Equal(ProcessorErrorCodes.MissingStepResult, exception.Code);
        Assert.Equal("Process result for step 'Test-Step' contained no entry for that step.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Step_WhenTheStepAppearsMoreThanOnce_ReportsTheEntryThatRan()
    {
        var registration = CreateRegistration();
        var completed = CreateProcessResult(registration.Metadata.Name, new TestResponse()).Steps.Single();
        var pending = completed with { ExecutionStatus = StepExecutionStatus.Pending, Response = null };
        var result = CreateProcessResult(registration.Metadata.Name, new TestResponse()) with
        {
            Steps = [pending, completed with { StepName = "test-step" }, pending],
        };
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

        var response = await CreateSut(CreateRegistry(registration))
            .ExecuteStepAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.Equal(result.ProcessId, response.ProcessId);
        Assert.Equal(result.Steps.Select(x => x.StepName), response.Steps.Select(x => x.StepName));
        Assert.Contains(response.Steps, step => step.ExecutionStatus == StepExecutionStatus.Completed && step.StepName == "test-step");
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
        var result = CreateProcessResult(registration.Metadata.Name, new TestResponse());
        ProcessRequest? captured = null;
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(result);
        var processId = Guid.NewGuid();

        var response = await CreateSut(CreateRegistry(registration), contextProcessId: processId)
            .ExecuteAsync(Request(registration.Metadata.Name, new { value = "abc" }), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(processId, captured.ProcessId);
        Assert.True(captured.Processor.Steps.ContainsKey(registration.Metadata.Name));
        Assert.Equal(result.ProcessId, response.ProcessId);
        Assert.Equal(result.Steps.Select(x => x.StepName), response.Steps.Select(x => x.StepName));
    }

    [Fact]
    public async Task ExecuteAsync_TypedStep_UsesRegistrationNameAndReturnsTypedResponse()
    {
        var registration = CreateRegistration();
        var result = CreateProcessResult(registration.Metadata.Name, new TestResponse());
        ProcessRequest? captured = null;
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(result);

        var response = await CreateSut(CreateRegistry(registration), contextProcessId: Guid.NewGuid())
            .ExecuteStepAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured.Processor.Steps.ContainsKey(registration.Metadata.Name));
        Assert.Equal(result.ProcessId, response.ProcessId);
        Assert.Equal(result.Steps.Select(x => x.StepName), response.Steps.Select(x => x.StepName));
        Assert.IsType<TestResponse>(Assert.Single(response.Steps).Response);
    }

    [Fact]
    public async Task ExecuteAsync_UntypedStep_UsesRegistrationNameAndReturnsResponse()
    {
        var registration = CreateRegistration();
        var result = CreateProcessResult(registration.Metadata.Name, new TestResponse());
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

        var response = await CreateSut(CreateRegistry(registration))
            .ExecuteStepAsync(new ExecuteStepRequest<TestStep> { ProcessStep = new TestStep() }, CancellationToken.None);

        Assert.Equal(result.ProcessId, response.ProcessId);
        Assert.Equal(result.Steps.Select(x => x.StepName), response.Steps.Select(x => x.StepName));
        Assert.Equal(registration.Metadata.Name, Assert.Single(response.Steps).StepName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepNotAuthorized_Throws()
    {
        var registration = CreateRegistration();
        var authorizer = new Mock<IKaleidoAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(It.IsAny<AuthorizationMetadata?>(), registration.Metadata.Name, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KaleidoAuthorizationException(registration.Metadata.Name, callerIsAuthenticated: true));

        await Assert.ThrowsAsync<KaleidoAuthorizationException>(() => CreateSut(CreateRegistry(registration), authorizer: authorizer.Object)
            .ExecuteAsync(Request(registration.Metadata.Name), CancellationToken.None));
        runtime.Verify(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAStepIsSubmittedTwice_RejectsTheRequestBeforeRunning()
    {
        var request = new ExecuteProcessRequest
        {
            Steps =
            [
                new ProcessStepRequest { StepName = "CaptureMember", Request = JsonSerializer.SerializeToElement(new { }) },
                new ProcessStepRequest { StepName = "capturemember", Request = JsonSerializer.SerializeToElement(new { }) },
            ],
        };

        var exception = await Assert.ThrowsAsync<KaleidoValidationException>(() => CreateSut().ExecuteAsync(request, CancellationToken.None));

        Assert.Equal(ProcessorErrorCodes.InvalidRequest, exception.Code);
        Assert.Contains("'capturemember'", exception.Message, StringComparison.Ordinal);
        runtime.Verify(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnySubmittedStepDenied_RejectsWholeRequestBeforeRunning()
    {
        var registration = CreateRegistration();
        var authorizer = new Mock<IKaleidoAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(It.IsAny<AuthorizationMetadata?>(), registration.Metadata.Name, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        authorizer.Setup(x => x.AuthorizeAsync(It.IsAny<AuthorizationMetadata?>(), "Denied", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KaleidoAuthorizationException("Denied", callerIsAuthenticated: false));
        var request = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest { StepName = registration.Metadata.Name, Request = JsonSerializer.SerializeToElement(new { }) },
                new ProcessStepRequest { StepName = "Denied", Request = JsonSerializer.SerializeToElement(new { }) }],
        };

        await Assert.ThrowsAsync<KaleidoAuthorizationException>(() => CreateSut(CreateRegistry(registration), authorizer: authorizer.Object)
            .ExecuteAsync(request, CancellationToken.None));
        runtime.Verify(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepUnknown_AuthorizesAsUndeclared()
    {
        var authorizer = new Mock<IKaleidoAuthorizer>();
        var exception = await Assert.ThrowsAsync<KaleidoValidationException>(() => CreateSut(authorizer: authorizer.Object)
            .ExecuteAsync(Request("NoSuchStep"), CancellationToken.None));

        // Authorized as undeclared first (fail closed), then rejected before anything runs (#344).
        authorizer.Verify(x => x.AuthorizeAsync(null, "NoSuchStep", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ProcessorErrorCodes.UnknownStep, exception.Code);
        Assert.Contains("'NoSuchStep'", exception.Message, StringComparison.Ordinal);
    }

    // /execute builds each step in the transport (KaleidoJsonOptions); core receives typed steps (#344).
    [Fact]
    public async Task ExecuteAsync_BuildsEachStepAsItsStepType_BeforeTheRuntime()
    {
        var registration = CreateRegistration(typeof(TypedInputStep), nameof(TypedInputStep));
        ProcessRequest? captured = null;
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(CreateProcessResult(nameof(TypedInputStep), new TestResponse()));
        var itemId = Guid.NewGuid();

        await CreateSut(CreateRegistry(registration)).ExecuteAsync(Request("typedinputstep", new { itemId }), TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        var step = Assert.IsType<TypedInputStep>(captured.Processor.Steps[nameof(TypedInputStep)]);
        Assert.Equal(itemId, step.ItemId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepInputIsOmitted_RejectsBeforeRunning()
    {
        var registration = CreateRegistration();
        var request = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest { StepName = registration.Metadata.Name, Request = default }],
        };

        var error = await Assert.ThrowsAsync<BadHttpRequestException>(() => CreateSut(CreateRegistry(registration))
            .ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Contains("Step 'Test-Step'", error.Message, StringComparison.Ordinal);
        runtime.Verify(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("{\"itemId\":\"not-a-guid\"}", "itemId")]
    [InlineData("{}", "itemId")]
    [InlineData("null", "no input")]
    public async Task ExecuteAsync_AStepThatCannotBeBuilt_IsRejected_NamingTheStep_AndNothingRuns(string input, string named)
    {
        var registration = CreateRegistration(typeof(TypedInputStep), nameof(TypedInputStep));
        using var json = JsonDocument.Parse(input);

        var exception = await Assert.ThrowsAsync<BadHttpRequestException>(() => CreateSut(CreateRegistry(registration))
            .ExecuteAsync(new ExecuteProcessRequest { Steps = [new ProcessStepRequest { StepName = nameof(TypedInputStep), Request = json.RootElement.Clone() }] },
                TestContext.Current.CancellationToken));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Contains("Step 'TypedInputStep'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(named, exception.Message, StringComparison.Ordinal);
        runtime.Verify(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoStepsSubmitted_AuthorizesAsUndeclared()
    {
        var authorizer = new Mock<IKaleidoAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(null, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProcessResult("any", new TestResponse()));

        await CreateSut(authorizer: authorizer.Object).ExecuteAsync(new ExecuteProcessRequest(), CancellationToken.None);

        authorizer.Verify(x => x.AuthorizeAsync(null, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ExecuteProcessRequest Request(string stepName, object? body = null) => new()
    {
        Steps = [new ProcessStepRequest { StepName = stepName, Request = JsonSerializer.SerializeToElement(body ?? new { }, KaleidoJsonOptions.Options) }],
    };

    private static ProcessResult CreateProcessResult(string stepName, object response) => new()
    {
        ProcessId = Guid.NewGuid(),
        State = ProcessExecutionState.Active,
        AvailableSteps = [stepName],
        Steps = [new ProcessStepResult
        {
            StepName = stepName,
            CandidateStatus = StepCandidateStatus.Built,
            IncludedInExecutionPlan = true,
            Response = response,
            ExecutionStatus = StepExecutionStatus.Completed,
        }],
    };

    private static IProcessorStepRegistry CreateRegistry(ProcessStepRegistration registration)
    {
        var registry = new Mock<IProcessorStepRegistry>();
        registry.Setup(x => x.GetRegistration(registration.StepType)).Returns(registration);
        registry.Setup(x => x.GetRegistration(registration.Metadata.Name)).Returns(registration);
        registry.Setup(x => x.Find(It.Is<string>(name => string.Equals(name, registration.Metadata.Name, StringComparison.OrdinalIgnoreCase)))).Returns(registration);
        return registry.Object;
    }

    private static ProcessStepRegistration CreateRegistration(Type? stepType = null, string name = "Test-Step") =>
        new(stepType ?? typeof(TestStep), typeof(TestResponse), typeof(TestStepHandler), [], [], [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = name, Description = "Test step", DisplayName = "Test Step" });

    public sealed record TestStep : IProcessStep;
    public sealed record TestResponse;

    public sealed class TestStepHandler : IProcessStepHandler<TestStep, TestResponse>
    {
        public Task<ProcessStepHandlerResult<TestResponse>> ExecuteAsync(TestStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    public sealed record TypedInputStep : IProcessStep
    {
        public required Guid ItemId { get; init; }
    }
}
