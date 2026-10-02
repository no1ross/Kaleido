using Kaleido.Processor.Context;
using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kaleido.Provider.SQLite;

internal sealed class SqliteProcessorContextStore(
    SqliteProcessorContextDbContext dbContext,
    KaleidoServiceOptions serviceOptions,
    ILogger<SqliteProcessorContextStore> logger)
    : IProcessorContextStore
{
    public async Task<ProcessorContext?> LoadAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = SqliteTelemetry.ActivitySource
            .StartActivity(SqliteTelemetry.LoadActivityName, ActivityKind.Internal);
        activity?.SetTag(SqliteTelemetry.TagProcessId, processId.ToString());

        try
        {
            var entity =
                await dbContext.ProcessContexts
                    .AsNoTracking()
                    .Include(x => x.Steps)
                    .Include(x => x.AvailableSteps)
                    .Include(x => x.RequiredStep)
                    .FirstOrDefaultAsync(
                        x => x.ProcessId ==
                             processId,
                        cancellationToken);

            if (entity is null)
            {
                return null;
            }

            return ToProcessorContext(
                entity,
                serviceOptions.ServiceName);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SqliteTelemetry.LoadFailuresCounter.Add(1);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            logger.LogError(
                exception,
                "Failed to load process context for process {ProcessId}.",
                processId);
            throw;
        }
    }

    public async Task SaveAsync(
        ProcessorContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            context);

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = SqliteTelemetry.ActivitySource
            .StartActivity(SqliteTelemetry.SaveActivityName, ActivityKind.Internal);
        activity?.SetTag(SqliteTelemetry.TagProcessId, context.ProcessId.ToString());

        try
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(
                    cancellationToken);

            var entity =
                await dbContext.ProcessContexts
                    .Include(x => x.Steps)
                    .Include(x => x.AvailableSteps)
                    .Include(x => x.RequiredStep)
                    .FirstOrDefaultAsync(
                        x => x.ProcessId ==
                             context.ProcessId,
                        cancellationToken);

            if (entity is null)
            {
                entity =
                    new ProcessorContextEntity
                    {
                        ProcessId =
                            context.ProcessId
                    };

                dbContext.ProcessContexts.Add(
                    entity);
            }

            entity.LatestRequestId =
                context.LatestRequestId;

            entity.Owner =
                context.Owner;

            entity.OwnerRoles =
                string.Join(",", context.OwnerRoles);

            entity.State =
                context.State;

            entity.CreatedUtc =
                context.CreatedUtc == default
                    ? DateTime.UtcNow
                    : context.CreatedUtc;

            entity.UpdatedUtc =
                context.UpdatedUtc == default
                    ? DateTime.UtcNow
                    : context.UpdatedUtc;

            SyncSteps(entity, context);
            SyncAvailableSteps(entity, context);
            SyncRequiredStep(entity, context, serviceOptions.ServiceName);

            await dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            logger.LogDebug(
                "Process context saved for process {ProcessId} ({StepCount} steps).",
                context.ProcessId,
                context.Steps.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SqliteTelemetry.SaveFailuresCounter.Add(1);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            logger.LogError(
                exception,
                "Failed to save process context for process {ProcessId}.",
                context.ProcessId);
            throw;
        }
    }

    // Update children in place so EF generates only the delta — no
    // delete-all/reinsert-all write amplification per save.
    private static void SyncSteps(
        ProcessorContextEntity entity,
        ProcessorContext context)
    {
        var incoming = context.Steps
            .ToDictionary(x => x.StepName, StringComparer.Ordinal);

        foreach (var row in entity.Steps.ToArray())
        {
            if (!incoming.TryGetValue(row.StepName, out var step))
            {
                entity.Steps.Remove(row);
                continue;
            }

            row.Version = step.Version;
            row.Status = step.Status;
            row.LatestRequestId = step.LatestRequestId;
            row.LastExecuted = step.LastExecuted;
        }

        var existing = entity.Steps
            .Select(x => x.StepName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var step in context.Steps)
        {
            if (existing.Contains(step.StepName))
            {
                continue;
            }

            entity.Steps.Add(
                new ProcessorStepContextEntity
                {
                    ProcessId = context.ProcessId,
                    StepName = step.StepName,
                    Version = step.Version,
                    Status = step.Status,
                    LatestRequestId = step.LatestRequestId,
                    LastExecuted = step.LastExecuted
                });
        }
    }

    private static void SyncAvailableSteps(
        ProcessorContextEntity entity,
        ProcessorContext context)
    {
        var incoming = context.AvailableSteps
            .ToHashSet(StringComparer.Ordinal);

        foreach (var row in entity.AvailableSteps.ToArray())
        {
            if (!incoming.Contains(row.StepName))
            {
                entity.AvailableSteps.Remove(row);
            }
        }

        var existing = entity.AvailableSteps
            .Select(x => x.StepName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (stepName, index) in context.AvailableSteps
                     .Select((name, i) => (name, i)))
        {
            var row = entity.AvailableSteps
                .FirstOrDefault(x => string.Equals(
                    x.StepName, stepName, StringComparison.Ordinal));

            if (row is not null)
            {
                row.Sequence = index;
                continue;
            }

            entity.AvailableSteps.Add(
                new ProcessorAvailableStepEntity
                {
                    ProcessId = context.ProcessId,
                    StepName = stepName,
                    Sequence = index
                });
        }
    }

    private static void SyncRequiredStep(
        ProcessorContextEntity entity,
        ProcessorContext context,
        string localProcessorName)
    {
        if (context.RequiredStep is null)
        {
            if (entity.RequiredStep is not null)
            {
                entity.RequiredStep = null;
            }

            return;
        }

        // Store the target processor name when cross-processor,
        // otherwise store the local processor name for backwards compatibility.
        var processorName =
            context.TargetProcessorName ?? localProcessorName;

        if (entity.RequiredStep is not null)
        {
            entity.RequiredStep.ProcessorName = processorName;
            entity.RequiredStep.StepName = context.RequiredStep;
            return;
        }

        entity.RequiredStep = new ProcessorRequiredStepEntity
        {
            ProcessId = context.ProcessId,
            ProcessorName = processorName,
            StepName = context.RequiredStep
        };
    }

    private static ProcessorContext ToProcessorContext(
        ProcessorContextEntity entity,
        string localProcessorName)
    {
        return new ProcessorContext
        {
            ProcessId =
                entity.ProcessId,

            ProcessorName =
                localProcessorName,

            LatestRequestId =
                entity.LatestRequestId,

            Owner =
                entity.Owner,

            OwnerRoles =
                string.IsNullOrWhiteSpace(entity.OwnerRoles)
                    ? []
                    : entity.OwnerRoles.Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries),

            State =
                entity.State,

            RequiredStep =
                entity.RequiredStep?.StepName,

            // When the stored processor name differs from the local processor,
            // this was a cross-processor handoff — surface it as TargetProcessorName.
            TargetProcessorName =
                entity.RequiredStep is null
                    ? null
                    : string.Equals(
                        entity.RequiredStep.ProcessorName,
                        localProcessorName,
                        StringComparison.OrdinalIgnoreCase)
                        ? null
                        : entity.RequiredStep.ProcessorName,

            CreatedUtc =
                entity.CreatedUtc,

            UpdatedUtc =
                entity.UpdatedUtc,

            // Available steps are always local step names.
            AvailableSteps =
                entity.AvailableSteps
                    .OrderBy(x => x.Sequence)
                    .Select(x => x.StepName)
                    .ToArray(),

            Steps =
                entity.Steps
                    .OrderBy(x => x.StepName, StringComparer.OrdinalIgnoreCase)
                    .Select(step =>
                        new StepContext
                        {
                            StepName =
                                step.StepName,

                            Version =
                                step.Version,

                            Status =
                                step.Status,

                            LatestRequestId =
                                step.LatestRequestId,

                            LastExecuted =
                                step.LastExecuted
                        })
                    .ToArray()
        };
    }
}
