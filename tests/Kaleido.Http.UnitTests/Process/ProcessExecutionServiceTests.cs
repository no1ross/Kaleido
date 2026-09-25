using System.Text.Json;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.UnitTests.Process;

internal sealed class ProcessExecutionServiceTests
    : SutFixture<ProcessExecutionService>
{
    protected override ProcessExecutionService CreateSut() =>
        CreateSut(
            Mock.Of<IProcessStepRegistry>(),
            Mock.Of<IProcessRuntime>(),
            Mock.Of<IProcessExecutionResponseFactory>());

    private static ProcessExecutionService CreateSut(
        IProcessStepRegistry registry,
        IProcessRuntime runtime,
        IProcessExecutionResponseFactory responseFactory,
        Guid? contextProcessId = null)
    {
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation
            .Setup(x => x.Current)
            .Returns(new KaleidoCorrelationContext
            {
                RequestId = Guid.NewGuid().ToString(),
                ProcessId = contextProcessId
            });

        return new ProcessExecutionService(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            registry,
            runtime,
            new KaleidoServiceOptions { ServiceName = "test-processor" },
            correlation.Object,
            responseFactory,
            NullLogger<ProcessExecutionService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessRequest_MapsRequestAndReturnsResponse()
    {
        var registration = CreateRegistration();
        var registry = CreateRegistry(registration);

        ProcessRequest? capturedRequest = null;
        var runtime = new Mock<IProcessRuntime>();
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

        Assert.Same(expectedResponse, response);
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
        var runtime = new Mock<IProcessRuntime>();

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
        Assert.Same(expectedResponse, response);
    }

    [Fact]
    public async Task ExecuteAsync_UntypedStep_UsesRegistrationNameAndReturnsResponse()
    {
        var registration = CreateRegistration();
        var registry = CreateRegistry(registration);

        var runtime = new Mock<IProcessRuntime>();

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

        Assert.Same(expectedResponse, response);
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
                    Outcome = StepExecutionOutcome.Completed,
                    RuntimeMessages = [],
                    BusinessMessages = []
                }
            ]
        };

    private static IProcessStepRegistry CreateRegistry(ProcessStepRegistration registration)
    {
        var registry = new Mock<IProcessStepRegistry>();

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
            new ProcessStepMetadata("Test-Step", "Test step", "1.0.0", "Test Step"));

    public sealed record TestStep;
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
