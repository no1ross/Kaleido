using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Execution;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Text.Json;

namespace Kaleido.Process.AspNetCore.Tests;

public sealed class ProcessExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ProcessRequest_MapsRequestAndReturnsResponse()
    {
        var registration =
            CreateRegistration();

        var registry =
            CreateRegistry(registration);

        ProcessRequest? capturedRequest = null;

        var runtime =
            new Mock<IProcessorRuntime>();

        var processResult =
            CreateProcessResult(
                registration.Metadata.Name,
                new TestResponse());

        runtime
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessRequest>(),
                    It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(processResult);

        var service =
            new ProcessExecutionService(
                new HttpContextAccessor
                {
                    HttpContext = new DefaultHttpContext()
                },
                registry,
                CreateProcessorRegistry(),
                runtime.Object,
                new ProcessRouteOptions());

        var request =
            new ExecuteProcessRequest
            {
                ProcessId = Guid.NewGuid(),
                Steps =
                [
                    new ProcessStepRequest
                    {
                        StepName = registration.Metadata.Name,
                        Request = JsonSerializer.SerializeToElement(new { value = "abc" })
                    }
                ]
            };

        var response =
            await service.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(request.ProcessId, capturedRequest.ProcessId);
        Assert.True(capturedRequest.Processor.Steps.ContainsKey(registration.Metadata.Name));

        Assert.Equal(processResult.ProcessId, response.ProcessId);
        Assert.Equal(registration.Metadata.Name, Assert.Single(response.Results).StepName);
        Assert.Equal(registration.Metadata.Name, Assert.Single(response.AvailableSteps).StepName);
    }

    [Fact]
    public async Task ExecuteAsync_TypedStep_UsesRegistrationNameAndReturnsTypedResponse()
    {
        var registration =
            CreateRegistration();

        var registry =
            CreateRegistry(registration);

        ProcessRequest? capturedRequest = null;

        var runtime =
            new Mock<IProcessorRuntime>();

        runtime
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessRequest>(),
                    It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(
                CreateProcessResult(registration.Metadata.Name, new TestResponse()));

        var service =
            new ProcessExecutionService(
                new HttpContextAccessor
                {
                    HttpContext = new DefaultHttpContext()
                },
                registry,
                CreateProcessorRegistry(),
                runtime.Object,
                new ProcessRouteOptions());

        var request =
            new ExecuteStepRequest<TestStep>
            {
                ProcessId = Guid.NewGuid(),
                ProcessStep = new TestStep()
            };

        var response =
            await service.ExecuteAsync<TestStep, TestResponse>(
                request,
                CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Processor.Steps.ContainsKey(registration.Metadata.Name));
        Assert.Equal(registration.Metadata.Name, response.StepName);
        Assert.NotNull(response.Result);
    }

    [Fact]
    public async Task ExecuteAsync_UntypedStep_UsesRegistrationNameAndReturnsResponse()
    {
        var registration =
            CreateRegistration();

        var registry =
            CreateRegistry(registration);

        var runtime =
            new Mock<IProcessorRuntime>();

        runtime
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateProcessResult(registration.Metadata.Name, new TestResponse()));

        var service =
            new ProcessExecutionService(
                new HttpContextAccessor
                {
                    HttpContext = new DefaultHttpContext()
                },
                registry,
                CreateProcessorRegistry(),
                runtime.Object,
                new ProcessRouteOptions());

        var request =
            new ExecuteStepRequest<TestStep>
            {
                ProcessId = Guid.NewGuid(),
                ProcessStep = new TestStep()
            };

        var response =
            await service.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(registration.Metadata.Name, response.StepName);
        Assert.Equal(StepExecutionOutcome.Completed, response.Outcome);
    }

    private static ProcessorProcessResult CreateProcessResult(
        string stepName,
        object response) =>
        new()
        {
            ProcessId = Guid.NewGuid(),
            State = ProcessExecutionState.Active,
            AvailableSteps = [new ProcessStepReference { ProcessorName = "test", StepName = stepName }],
            Steps =
            [
                new ProcessorStepResult
                {
                    StepName = stepName,
                    CandidateStatus = StepCandidateStatus.Built,
                    IncludedInExecutionPlan = true,
                    Response = response,
                    ExecutionStatus = StepExecutionStatus.Completed,
                    Decision = ExecutionDecisionType.Complete,
                    Outcome = StepExecutionOutcome.Completed,
                    RuntimeMessages = [],
                    BusinessMessages = []
                }
            ]
        };

    private static IProcessStepRegistry CreateRegistry(
        ProcessStepRegistration registration)
    {
        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .Setup(x => x.GetRegistration(typeof(TestStep)))
            .Returns(registration);

        registry
            .Setup(x => x.GetRegistration(registration.Metadata.Name))
            .Returns(registration);

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
            new RepeatableOptions
            {
                Enabled = false
            },
            new ProcessStepMetadata(
                "Test-Step",
                "Test step",
                "1.0.0",
                "Test Step"));

    private static IProcessorRegistry CreateProcessorRegistry()
    {
        var registry = new Mock<IProcessorRegistry>();

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
                    InstanceId = Guid.NewGuid(),
                    InitialSteps = [],
                    Steps = []
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
