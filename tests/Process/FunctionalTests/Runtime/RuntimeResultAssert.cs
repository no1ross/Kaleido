using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Tests.Runtime;

internal static class RuntimeResultAssert
{
    public static ParticipantStepResult Step(
        ParticipantProcessResult result,
        string stepName)
    {
        return result.Steps.Single(x =>
            string.Equals(
                x.StepName,
                stepName,
                StringComparison.OrdinalIgnoreCase));
    }

    public static void StepCompleted(
        ParticipantProcessResult result,
        string stepName)
    {
        var step = Step(result, stepName);

        Assert.Equal(
            StepExecutionStatus.Completed,
            step.ExecutionStatus);
    }

    public static void HasMessage(
        ParticipantProcessResult result,
        StepProcessingMessageCode code)
    {
        Assert.Contains(
            result.Steps.SelectMany(x => x.Messages),
            x => x.Code == code);
    }

    public static void HasErrorMessage(
        ParticipantProcessResult result,
        StepProcessingMessageCode code)
    {
        Assert.Contains(
            result.Steps.SelectMany(x => x.Messages),
            x => x.Type == MessageType.Error &&
                 x.Code == code);
    }

    public static void AvailableStep(
        ParticipantProcessResult result,
        string stepName)
    {
        Assert.Contains(
            result.AvailableSteps,
            x => string.Equals(
                x,
                stepName,
                StringComparison.OrdinalIgnoreCase));
    }
}
