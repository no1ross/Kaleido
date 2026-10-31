using Kaleido.Processor.Context;
using Kaleido.Processor.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Processor.Recovery;

// Saves the step outcomes Kaleido could not save once the store is back (#537), on each instance for
// its own. A save is re-applied exactly as it was attempted (the context with its base version, the
// step's event): the version check makes it safe - if the process moved meanwhile, the outcome is
// parked for an operator, never applied blindly. Any other failure is retried with backoff, then parked.
internal sealed class ProcessorRecoveryService(
    IServiceScopeFactory scopeFactory,
    IProcessorRecoveryStore store,
    TimeProvider time)
    : BackgroundService
{
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RecoverDueAsync(stoppingToken);
                await Task.Delay(PollInterval, time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping: what is still pending was logged when it was kept.
        }
    }

    // Attempts every outcome that is due (a stopping host stops between attempts, never in one);
    // returns how many were attempted.
    internal async Task<int> RecoverDueAsync(CancellationToken cancellationToken)
    {
        var due = store.Due(time.GetUtcNow());

        foreach (var outcome in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RecoverAsync(outcome);
        }

        return due.Count;
    }

    // An attempt is never cancelled: a stopping host would otherwise abandon a save half-way. So no
    // cancellation reaches the catch-all below; an OperationCanceledException there is a failure (#517).
    private async Task RecoverAsync(PendingOutcome outcome)
    {
        var saveToken = CancellationToken.None;

        await using var scope = scopeFactory.CreateAsyncScope();
        using var observation =
            scope.ServiceProvider.GetRequiredService<IProcessorObservability>().BeginRecovery(
                new ProcessorRecoveryObservationDetails(outcome.Context.ProcessId, outcome.StepName, outcome.Attempts + 1));

        try
        {
            await scope.ServiceProvider.GetRequiredService<IProcessorTransition>().SaveAsync(outcome.Context, outcome.TransitionEvent, saveToken);

            store.Remove(outcome.Context.ProcessId);
            observation.Recovered();
        }
        catch (KaleidoConcurrencyException exception)
        {
            store.Remove(outcome.Context.ProcessId);
            observation.Parked("conflict", exception, outcome);
        }
        catch (OperationCanceledException) when (saveToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var now = time.GetUtcNow();

            if (now - outcome.RecordedUtc >= MaxAge)
            {
                store.Remove(outcome.Context.ProcessId);
                observation.Parked("expired", exception, outcome);
                return;
            }

            var attempts = outcome.Attempts + 1;
            store.Reschedule(outcome with { Attempts = attempts, NextAttemptUtc = now + RetryDelay(attempts) });
            observation.Retrying(exception);
        }
    }

    // 1 s, 2 s, 4 s ... up to MaxRetryDelay.
    internal static TimeSpan RetryDelay(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Clamp(attempts - 1, 0, 16)), MaxRetryDelay.TotalSeconds));
}
