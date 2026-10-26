namespace Kaleido.Http.FunctionalTests.Processor.Cancellation;

// Steps whose handlers cancel the request at a chosen moment. The models live in their own
// namespace: only CancellationFinalizationTests registers them.
//   CancellationStartStep   initial; starts the case
//   CancelDuringStep        cancels the request, then observes it (throws)
//   CancelAfterStep         cancels the request, then returns success (the outcome is final)
internal static class CancellationSteps
{
    public const string Start = nameof(CancellationStartStep);
    public const string During = nameof(CancelDuringStep);
    public const string After = nameof(CancelAfterStep);
}

[ProcessStep(DisplayName = "Start", Description = "Starts the case.")]
public sealed record CancellationStartStep : IProcessStep;

[ProcessStep(DisplayName = "Cancel during", Description = "Cancels the request and observes it.")]
[DependsOn<CancellationStartStep>]
public sealed record CancelDuringStep : IProcessStep;

[ProcessStep(DisplayName = "Cancel after", Description = "Cancels the request and returns success.")]
[DependsOn<CancellationStartStep>]
public sealed record CancelAfterStep : IProcessStep;

// The request's cancellation, controlled by the test: a middleware makes RequestAborted this
// request's token (cancelled up front when the test asks for it), and handlers cancel it.
public sealed class RequestCancellation : IDisposable
{
    private CancellationTokenSource _current = new();
    private int _invocations;

    public bool CancelBeforeHandling { get; set; }

    public int Invocations => Volatile.Read(ref _invocations);

    // The tests send one request at a time, so the previous request's source is no longer in use.
    public CancellationToken Begin()
    {
        _current.Dispose();
        _current = new CancellationTokenSource();

        if (CancelBeforeHandling)
        {
            _current.Cancel();
        }

        return _current.Token;
    }

    public Task CancelAsync()
    {
        Interlocked.Increment(ref _invocations);
        return _current.CancelAsync();
    }

    public void Dispose() => _current.Dispose();
}

public sealed class CancellationStartHandler : IProcessStepHandler<CancellationStartStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(CancellationStartStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class CancelDuringHandler(RequestCancellation cancellation) : IProcessStepHandler<CancelDuringStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(CancelDuringStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default)
    {
        await cancellation.CancelAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return ProcessStepHandlerResult.Success();
    }
}

public sealed class CancelAfterHandler(RequestCancellation cancellation) : IProcessStepHandler<CancelAfterStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(CancelAfterStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default)
    {
        await cancellation.CancelAsync();
        return ProcessStepHandlerResult.Success();
    }
}
