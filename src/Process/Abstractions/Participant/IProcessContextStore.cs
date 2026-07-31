using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant;

public interface IProcessContextStore
{
    Task<ParticipantContext> LoadAsync(string? correlationId, CancellationToken cancellationToken = default);

    Task SaveAsync(ParticipantContext context, CancellationToken cancellationToken = default);
}

public sealed class InMemoryProcessContextStore
    : IProcessContextStore
{
    private readonly ConcurrentDictionary<string, ParticipantContext> _contexts =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<ParticipantContext> LoadAsync(string? correlationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return Task.FromResult(
                new ParticipantContext
                {
                    CorrelationId = Guid.NewGuid().ToString("N")
                });
        }

        if (_contexts.TryGetValue(
            correlationId,
            out var context))
        {
            return Task.FromResult(context);
        }

        return Task.FromResult(
            new ParticipantContext
            {
                CorrelationId = correlationId
            });
    }

    public Task SaveAsync(ParticipantContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(context.CorrelationId))
        {
            throw new InvalidOperationException(
                "ParticipantContext must contain a CorrelationId before it can be saved.");
        }

        _contexts[context.CorrelationId] = context;

        return Task.CompletedTask;
    }
}

public sealed record ParticipantContext
{
    public string? CorrelationId { get; init; }

    public IReadOnlyCollection<ProcessStepContext> ProcessSteps { get; init; }
        = [];

    public ParticipantContext AddStepContext(ProcessStepContext step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return this with
        {
            ProcessSteps =
            [
                .. ProcessSteps,
                step
            ]
        };
    }

    public ProcessStepContext? FindStep(string stepName)
    {
        return ProcessSteps.FirstOrDefault(
            x => string.Equals(
                x.StepName,
                stepName,
                StringComparison.OrdinalIgnoreCase));
    }

    public bool HasCompletedStep(string stepName)
    {
        return ProcessSteps.Any(
            x => string.Equals(
                x.StepName,
                stepName,
                StringComparison.OrdinalIgnoreCase)
            && x.Outcome == ProcessStepOutcome.Completed);
    }
}

public sealed record ProcessStepContext
{
    public string StepName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public IReadOnlyCollection<ProcessStepRequestContext> Requests { get; init; }
        = [];

    public ProcessStepOutcome Outcome { get; init; }

    public DateTimeOffset LastProcessed { get; init; }

    public ProcessStepContext AddStepRequestContext(ProcessStepRequestContext step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return this with
        {
            Requests =
            [
                .. Requests,
                step
            ]
        };
    }
}

public sealed record ProcessStepRequestContext
{
    public IDictionary<string, object?>? Request { get; init; }

    public ProcessStepStatus Status { get; init; }

    public DateTimeOffset ProcessedOn { get; init; }

    public IReadOnlyCollection<ProcessStepMessage> Messages { get; init; }
        = [];
}