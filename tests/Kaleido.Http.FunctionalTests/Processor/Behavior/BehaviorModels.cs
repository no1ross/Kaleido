using System.ComponentModel.DataAnnotations;

namespace Kaleido.Http.FunctionalTests.Processor.Behavior;

// A small processor whose rules exercise every relationship and handler outcome the runtime offers:
//   Start                       initial, not repeatable
//   Note     [AvailableUntil<Close>] [Repeatable]  validated input; "reject" is rejected as correctable
//   Close    [DependsOn<Start>]
//   Archive  [AvailableAfter<Close>]
//   Explode  [DependsOn<Start>] [Repeatable]   throws while BehaviorSwitches.Explode is set
//   HandOff  [DependsOn<Start>]                hands the process to "radiology"
//   Slow     [DependsOn<Start>] [Repeatable]   waits for BehaviorGate (to race two saves)
//   Quick    [DependsOn<Start>] [Repeatable]
// The models live in their own namespace: only BehaviorFixture registers them.
internal static class BehaviorSteps
{
    public const string Start = nameof(BehaviorStartStep);
    public const string Note = nameof(BehaviorNoteStep);
    public const string Close = nameof(BehaviorCloseStep);
    public const string Archive = nameof(BehaviorArchiveStep);
    public const string Explode = nameof(BehaviorExplodeStep);
    public const string HandOff = nameof(BehaviorHandOffStep);
    public const string Slow = nameof(BehaviorSlowStep);
    public const string Quick = nameof(BehaviorQuickStep);

    public const string RejectedNote = "reject";
    public const string RejectionCode = "BEHAVIOR_NOTE_REJECTED";
    public const string ExplosionDetail = "internal detail 4711";
    public const string HandOffTarget = "radiology";
}

[ProcessStep(DisplayName = "Start", Description = "Starts the case.", Version = "1.0")]
public sealed record BehaviorStartStep : IProcessStep;

[ProcessStep(DisplayName = "Note", Description = "Adds a note while the case is open.", Version = "1.0")]
[AvailableUntil<BehaviorCloseStep>]
[Repeatable]
public sealed record BehaviorNoteStep : IProcessStep
{
    [Required]
    [StringLength(10)]
    public string Text { get; init; } = string.Empty;
}

[ProcessStep(DisplayName = "Close", Description = "Closes the case.", Version = "1.0")]
[DependsOn<BehaviorStartStep>]
public sealed record BehaviorCloseStep : IProcessStep;

[ProcessStep(DisplayName = "Archive", Description = "Archives a closed case.", Version = "1.0")]
[AvailableAfter<BehaviorCloseStep>]
public sealed record BehaviorArchiveStep : IProcessStep;

[ProcessStep(DisplayName = "Explode", Description = "Fails unexpectedly while switched on.", Version = "1.0")]
[DependsOn<BehaviorStartStep>]
[Repeatable]
public sealed record BehaviorExplodeStep : IProcessStep;

[ProcessStep(DisplayName = "Hand off", Description = "Hands the case to radiology.", Version = "1.0")]
[DependsOn<BehaviorStartStep>]
public sealed record BehaviorHandOffStep : IProcessStep;

[ProcessStep(DisplayName = "Slow", Description = "Waits for the gate.", Version = "1.0")]
[DependsOn<BehaviorStartStep>]
[Repeatable]
public sealed record BehaviorSlowStep : IProcessStep;

[ProcessStep(DisplayName = "Quick", Description = "Completes at once.", Version = "1.0")]
[DependsOn<BehaviorStartStep>]
[Repeatable]
public sealed record BehaviorQuickStep : IProcessStep;

public sealed class BehaviorSwitches
{
    private volatile bool _explode = true;

    public bool Explode
    {
        get => _explode;
        set => _explode = value;
    }
}

// Holds a Slow step inside its handler until released, so a second request can save first.
public sealed class BehaviorGate
{
    public TaskCompletionSource Entered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Reset()
    {
        Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class BehaviorRecord : IQueryContext
{
    [Filterable(FilterOperator.Equals)]
    public int Id { get; init; }
}

// A query source with a defect: the failure must reach the caller as a bare 500, never its detail.
[QuerySource(Version = "1.0", DisplayName = "Broken records", Description = "A source that fails unexpectedly.")]
public sealed class BehaviorBrokenSource : IQuerySource<BehaviorRecord>
{
    public IQueryable<BehaviorRecord> CreateQuery(QueryExecutionContext executionContext) =>
        throw new InvalidOperationException(BehaviorSteps.ExplosionDetail);
}

public sealed class BehaviorStartHandler : IProcessStepHandler<BehaviorStartStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(BehaviorStartStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class BehaviorNoteHandler : IProcessStepHandler<BehaviorNoteStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(BehaviorNoteStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            processStep.Text == BehaviorSteps.RejectedNote
                ? ProcessStepHandlerResult.Reject(new ProcessMessage { Code = BehaviorSteps.RejectionCode, Type = MessageType.Error, Message = "That note is not accepted; write another." })
                : ProcessStepHandlerResult.Success());
}

public sealed class BehaviorCloseHandler : IProcessStepHandler<BehaviorCloseStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(BehaviorCloseStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class BehaviorArchiveHandler : IProcessStepHandler<BehaviorArchiveStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(BehaviorArchiveStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class BehaviorExplodeHandler(BehaviorSwitches switches) : IProcessStepHandler<BehaviorExplodeStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(BehaviorExplodeStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        switches.Explode
            ? throw new InvalidOperationException(BehaviorSteps.ExplosionDetail)
            : Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class BehaviorHandOffHandler : IProcessStepHandler<BehaviorHandOffStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(BehaviorHandOffStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.HandOff(BehaviorSteps.HandOffTarget));
}

public sealed class BehaviorSlowHandler(BehaviorGate gate) : IProcessStepHandler<BehaviorSlowStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(BehaviorSlowStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default)
    {
        gate.Entered.TrySetResult();
        await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        return ProcessStepHandlerResult.Success();
    }
}

public sealed class BehaviorQuickHandler : IProcessStepHandler<BehaviorQuickStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(BehaviorQuickStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
