using System.Text.Json;
using Kaleido.Exceptions;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessExecutionResponseFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private static ProcessExecutionResponseFactory CreateSut(
        KaleidoHttpOptions? options = null,
        IProcessorResponseFactory? responseFactory = null) =>
        new(responseFactory ?? Mock.Of<IProcessorResponseFactory>(), options ?? new KaleidoHttpOptions());

    [Fact]
    public void CreateExecutionResponse_MapsRequiredAndAvailableStepsThroughTheResponseFactory()
    {
        var (responseFactory, required, available) = CreateResponseFactory();
        var processResult = CreateProcessResult(CreateStepResult()) with
        {
            RequiredStep = "Step-B",
            AvailableSteps = ["Step-A"],
        };

        var response = CreateSut(responseFactory: responseFactory.Object)
            .CreateExecutionResponse(processResult, CreateRegistry("Step-A", "Step-B"), "test");

        Assert.Equal(processResult.ProcessId, response.ProcessId);
        Assert.Same(required, response.RequiredStep);
        Assert.Same(available, Assert.Single(response.AvailableSteps));
        Assert.Null(response.TargetProcessorName);
        responseFactory.Verify(x => x.CreateStepSummary(It.Is<ProcessorStepSummary>(s => s.Name == "Step-A"), "test"), Times.Once);
    }

    [Fact]
    public void CreateStepResponse_MapsRequiredAndAvailableStepsThroughTheResponseFactory()
    {
        var (responseFactory, required, available) = CreateResponseFactory();
        var step = CreateStepResult();
        var processResult = CreateProcessResult(step) with
        {
            RequiredStep = "Step-B",
            AvailableSteps = ["Step-A"],
        };

        var response = CreateSut(responseFactory: responseFactory.Object)
            .CreateStepResponse(processResult, step, CreateRegistry("Step-A", "Step-B"), "test");

        Assert.Equal("test-step", response.StepName);
        Assert.Equal(StepExecutionOutcome.Failed, response.Outcome);
        Assert.Same(required, response.RequiredStep);
        Assert.Same(available, Assert.Single(response.AvailableSteps));
    }

    [Fact]
    public void CreateExecutionResponse_WhenHandedOff_HasNoRequiredStep()
    {
        var responseFactory = new Mock<IProcessorResponseFactory>();
        var processResult = CreateProcessResult(CreateStepResult()) with
        {
            RequiredStep = "Step-B",
            TargetProcessorName = "radiology",
        };

        var response = CreateSut(responseFactory: responseFactory.Object)
            .CreateExecutionResponse(processResult, CreateRegistry("Step-B"), "test");

        Assert.Null(response.RequiredStep);
        Assert.Equal("radiology", response.TargetProcessorName);
        responseFactory.Verify(
            x => x.CreateRequiredStep(It.IsAny<ProcessorStepSummary>(), It.IsAny<InformationRequest?>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void CreateExecutionResponse_WhenTheRequiredStepIsNotRegistered_ThrowsAFrameworkError()
    {
        var processResult = CreateProcessResult(CreateStepResult()) with { RequiredStep = "Ghost-Step" };

        var exception = Assert.Throws<KaleidoFrameworkException>(() =>
            CreateSut().CreateExecutionResponse(processResult, CreateRegistry(), "test"));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
        Assert.Equal("Required step 'Ghost-Step' was not found in the local registry.", exception.Message);
    }

    [Fact]
    public void CreateExecutionResponse_WhenAnAvailableStepIsNotRegistered_ThrowsAFrameworkError()
    {
        var processResult = CreateProcessResult(CreateStepResult()) with { AvailableSteps = ["Ghost-Step"] };

        var exception = Assert.Throws<KaleidoFrameworkException>(() =>
            CreateSut().CreateExecutionResponse(processResult, CreateRegistry(), "test"));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
        Assert.Equal("Available step 'Ghost-Step' was not found in the local registry.", exception.Message);
    }

    [Fact]
    public void CreateStepResponse_WhenAnAvailableStepIsNotRegistered_ThrowsAFrameworkError()
    {
        var step = CreateStepResult();
        var processResult = CreateProcessResult(step) with { AvailableSteps = ["Ghost-Step"] };

        var exception = Assert.Throws<KaleidoFrameworkException>(() =>
            CreateSut().CreateStepResponse(processResult, step, CreateRegistry(), "test"));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
    }

    [Fact]
    public void CreateExecutionResponse_ReportsOnlyStepsThatRanOrCarryMessages()
    {
        var pendingSilent = CreateStepResult() with
        {
            StepName = "silent",
            ExecutionStatus = StepExecutionStatus.Pending,
            BusinessMessages = [],
            RuntimeMessages = [],
        };
        var pendingWithRuntimeMessage = pendingSilent with
        {
            StepName = "runtime",
            RuntimeMessages = [StepProcessingMessage.Error(ProcessorErrorCodes.ValidationFailed, "Invalid input.")],
        };
        var pendingWithBusinessMessage = pendingSilent with
        {
            StepName = "business",
            BusinessMessages = [new ProcessMessage { Code = "note", Type = MessageType.Information, Message = "Note." }],
        };
        var completedWithoutResponse = pendingSilent with
        {
            StepName = "completed",
            ExecutionStatus = StepExecutionStatus.Completed,
            Response = null,
        };
        var processResult = CreateProcessResult(pendingSilent) with
        {
            Steps = [pendingSilent, pendingWithRuntimeMessage, pendingWithBusinessMessage, completedWithoutResponse],
        };

        var response = CreateSut().CreateExecutionResponse(processResult, CreateRegistry(), "test");

        Assert.Equal(["runtime", "business", "completed"], response.Results.Select(x => x.StepName));
        Assert.Equal("{}", JsonSerializer.Serialize(response.Results.Last().Response));
    }

    private static (Mock<IProcessorResponseFactory> Factory, ProcessRequiredStep Required, ProcessStepSummary Available) CreateResponseFactory()
    {
        var required = new ProcessRequiredStep { Name = "Step-B" };
        var available = new ProcessStepSummary { Name = "Step-A" };
        var responseFactory = new Mock<IProcessorResponseFactory>();
        responseFactory
            .Setup(x => x.CreateRequiredStep(It.Is<ProcessorStepSummary>(s => s.Name == "Step-B"), It.IsAny<InformationRequest?>(), "test"))
            .Returns(required);
        responseFactory
            .Setup(x => x.CreateStepSummary(It.Is<ProcessorStepSummary>(s => s.Name == "Step-A"), "test"))
            .Returns(available);
        return (responseFactory, required, available);
    }

    private static IProcessorStepRegistry CreateRegistry(params string[] stepNames)
    {
        var registry = new Mock<IProcessorStepRegistry>();

        foreach (var name in stepNames)
        {
            registry
                .Setup(x => x.Find(name))
                .Returns(new ProcessStepRegistration(
                    typeof(object),
                    null,
                    typeof(object),
                    [],
                    [],
                    [],
                    new RepeatableOptions { Enabled = false },
                    new ProcessStepMetadata { Name = name, Description = name, Version = "1.0.0", DisplayName = name }));
        }

        return registry.Object;
    }

    [Fact]
    public void Constructor_CreatesInstance()
    {
        Assert.NotNull(CreateSut());
    }

    [Fact]
    public void CreateStepResponse_ByDefault_SeparatesBusinessAndEmptyFrameworkMessages()
    {
        var step = CreateStepResult();
        var response = CreateSut().CreateStepResponse(
            CreateProcessResult(step), step, Mock.Of<IProcessorStepRegistry>(), "test");

        Assert.Equal("business_warning", Assert.Single(response.BusinessMessages).Code);
        Assert.Empty(response.FrameworkMessages);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, KaleidoJsonOptions.Options));
        Assert.True(json.RootElement.TryGetProperty("businessMessages", out _));
        Assert.True(json.RootElement.TryGetProperty("frameworkMessages", out var frameworkMessages));
        Assert.Equal(JsonValueKind.Array, frameworkMessages.ValueKind);
        Assert.Equal(0, frameworkMessages.GetArrayLength());
        Assert.False(json.RootElement.TryGetProperty("messages", out _));
    }

    [Fact]
    public void CreateExecutionResponse_WhenEnabled_SeparatesFrameworkAndBusinessMessages()
    {
        var step = CreateStepResult();
        var response = CreateSut(new KaleidoHttpOptions { IncludeFrameworkMessages = true })
            .CreateExecutionResponse(CreateProcessResult(step), Mock.Of<IProcessorStepRegistry>(), "test");

        var result = Assert.Single(response.Results);
        Assert.Equal("business_warning", Assert.Single(result.BusinessMessages).Code);
        Assert.Equal(ProcessorErrorCodes.ValidationFailed, Assert.Single(result.FrameworkMessages).Code);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, KaleidoJsonOptions.Options));
        Assert.True(json.RootElement.TryGetProperty("frameworkMessages", out _));
        Assert.False(json.RootElement.TryGetProperty("messages", out _));
    }

    [Fact]
    public void CreateStepResponse_WhenEnabled_PreservesCustomFrameworkCode()
    {
        var step = CreateStepResult() with
        {
            RuntimeMessages =
            [
                StepProcessingMessage.Error("pro_custom_validation", "Member invalid.")
            ]
        };

        var response = CreateSut(new KaleidoHttpOptions { IncludeFrameworkMessages = true })
            .CreateStepResponse(CreateProcessResult(step), step, Mock.Of<IProcessorStepRegistry>(), "test");

        Assert.Equal("pro_custom_validation", Assert.Single(response.FrameworkMessages).Code);
    }

    [Fact]
    public void CreateTypedStepResponse_WhenEnabled_KeepsBothMessageStreams()
    {
        var step = CreateStepResult();
        var response = CreateSut(new KaleidoHttpOptions { IncludeFrameworkMessages = true })
            .CreateStepResponse<string>(CreateProcessResult(step), step, Mock.Of<IProcessorStepRegistry>(), "test");

        Assert.Equal("payload", response.Result);
        Assert.Equal("business_warning", Assert.Single(response.BusinessMessages).Code);
        Assert.Equal(ProcessorErrorCodes.ValidationFailed, Assert.Single(response.FrameworkMessages).Code);
    }

    private static ProcessResult CreateProcessResult(ProcessStepResult step) =>
        new()
        {
            ProcessId = Guid.NewGuid(),
            State = ProcessExecutionState.Active,
            Steps = [step]
        };

    private static ProcessStepResult CreateStepResult() =>
        new()
        {
            StepName = "test-step",
            CandidateStatus = StepCandidateStatus.Built,
            IncludedInExecutionPlan = true,
            ExecutionStatus = StepExecutionStatus.ValidationFailed,
            Outcome = StepExecutionOutcome.Failed,
            Response = "payload",
            BusinessMessages =
            [
                new ProcessMessage
                {
                    Code = "business_warning",
                    Type = MessageType.Warning,
                    Message = "Business warning."
                }
            ],
            RuntimeMessages =
            [
                StepProcessingMessage.Error(
                    ProcessorErrorCodes.ValidationFailed,
                    "Invalid input.")
            ]
        };
}
