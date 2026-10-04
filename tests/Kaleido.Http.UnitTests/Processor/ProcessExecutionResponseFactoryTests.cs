using System.Text.Json;
using Kaleido.Http.Processor;

namespace Kaleido.Http.UnitTests.Process;

public sealed class ProcessExecutionResponseFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private static ProcessExecutionResponseFactory CreateSut(
        KaleidoHttpOptions? options = null) =>
        new(Mock.Of<IProcessorResponseFactory>(), options ?? new KaleidoHttpOptions());

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
