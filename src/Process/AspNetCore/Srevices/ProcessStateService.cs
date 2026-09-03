using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.Context;
using Kaleido.Process.Execution;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessStateService
{
    Task<ProcessorProcessView?> GetCurrentState(
        Guid processId,
        CancellationToken cancellationToken);
}

public class ProcessStateService(IProcessContextStore contextStore)
    : IProcessStateService
{
    public async Task<ProcessorProcessView?> GetCurrentState(Guid processId, CancellationToken cancellationToken)
    {
        var context = await contextStore.LoadAsync(processId, cancellationToken);
        if (context == null) return null;
        return ProcessorProcessViewMapper.ToView(context);
    }
}


public sealed record ProcessorProcessView
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

    public IReadOnlyCollection<ProcessorProcessStepView> Steps
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessorProcessStepView
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

internal static class ProcessorProcessViewMapper
{
    public static ProcessorProcessView ToView(
        ProcessorContext context)
    {
        return new ProcessorProcessView
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
                        new ProcessorProcessStepView
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