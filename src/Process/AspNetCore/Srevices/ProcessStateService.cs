using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessStateService
{
    Task<ParticipantProcessView?> GetCurrentState(
        Guid processId,
        CancellationToken cancellationToken);
}

public class ProcessStateService(IProcessContextStore contextStore)
    : IProcessStateService
{
    public async Task<ParticipantProcessView?> GetCurrentState(Guid processId, CancellationToken cancellationToken)
    {
        var context = await contextStore.LoadAsync(processId, cancellationToken);
        if (context == null) return null;
        return ParticipantProcessViewMapper.ToView(context);
    }
}


public sealed record ParticipantProcessView
{
    public Guid ProcessId
    {
        get;
        init;
    }

    public ProcessExecutionState State
    {
        get;
        init;
    }

    public string? RequiredStep
    {
        get;
        init;
    }

    public IReadOnlyCollection<string> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public DateTimeOffset CreatedUtc
    {
        get;
        init;
    }

    public DateTimeOffset UpdatedUtc
    {
        get;
        init;
    }

    public IReadOnlyCollection<ParticipantProcessStepView> Steps
    {
        get;
        init;
    }
        = [];
}

public sealed record ParticipantProcessStepView
{
    public string StepName
    {
        get;
        init;
    } = string.Empty;

    public string Version
    {
        get;
        init;
    } = string.Empty;

    public StepExecutionStatus Status
    {
        get;
        init;
    }

    public DateTimeOffset? LastExecuted
    {
        get;
        init;
    }
}

internal static class ParticipantProcessViewMapper
{
    public static ParticipantProcessView ToView(
        ParticipantContext context)
    {
        return new ParticipantProcessView
        {
            ProcessId =
                context.ProcessId,

            State =
                context.State,

            RequiredStep =
                context.RequiredStep,

            AvailableSteps =
                context.AvailableSteps,

            CreatedUtc =
                context.CreatedUtc,

            UpdatedUtc =
                context.UpdatedUtc,

            Steps =
                context.Steps
                    .OrderBy(x => x.StepName)
                    .Select(x =>
                        new ParticipantProcessStepView
                        {
                            StepName = x.StepName,
                            Version = x.Version,
                            Status = x.Status,
                            LastExecuted = x.LastExecuted
                        })
                    .ToArray()
        };
    }
}