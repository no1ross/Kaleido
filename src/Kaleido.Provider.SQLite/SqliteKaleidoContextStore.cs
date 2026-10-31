using System.Text.Json;
using System.Text.Json.Serialization;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Kaleido.Provider.SQLite.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kaleido.Provider.SQLite;

internal sealed partial class SqliteKaleidoContextStore(
    SqliteKaleidoContextDbContext dbContext,
    KaleidoServiceOptions serviceOptions,
    ILogger<SqliteKaleidoContextStore> logger)
    : IKaleidoContextStore
{
    public async Task<ProcessorContext?> LoadAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = SqliteTelemetry.ActivitySource
            .StartActivity(SqliteTelemetry.LoadActivityName, ActivityKind.Internal);
        activity?.SetTag(KaleidoTelemetryTags.ProcessId, processId.ToString());

        try
        {
            var entity =
                await dbContext.ProcessContexts
                    .AsNoTracking()
                    .Include(x => x.Steps)
                    .Include(x => x.AvailableSteps)
                    .Include(x => x.RequiredStep)
                    .AsSplitQuery()
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
        // Only the caller's cancellation is not a load failure (#517).
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            SqliteTelemetry.LoadFailuresCounter.Add(1);
            // The type, never the driver's message (SQL, connection details) (#290).
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            LogLoadFailed(logger, exception, processId);
            throw;
        }
    }

    public async Task<ProcessorContext> SaveAsync(
        ProcessorContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            context);

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = SqliteTelemetry.ActivitySource
            .StartActivity(SqliteTelemetry.SaveActivityName, ActivityKind.Internal);
        activity?.SetTag(KaleidoTelemetryTags.ProcessId, context.ProcessId.ToString());

        // Inside a transition, the transaction SqliteKaleidoTransactions opened on this DbContext covers
        // the save (and the outbox's rows), and its rollback forgets the tracked changes; on its own, the
        // save opens one, and forgets them itself when it fails (#513).
        var ownTransaction = dbContext.Database.CurrentTransaction is null;
        var saved = false;

        try
        {
            await using var transaction = ownTransaction ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
            var result = await SaveTrackedAsync(context, cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            saved = true;
            LogSaved(logger, context.ProcessId, context.Steps.Count);

            return result;
        }
        // A conflict is not a store failure: the request lost the race and the caller rereads (409).
        catch (Exception exception) when (exception is not KaleidoConcurrencyException && (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested))
        {
            SqliteTelemetry.SaveFailuresCounter.Add(1);
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            LogSaveFailed(logger, exception, context.ProcessId);
            throw;
        }
        finally
        {
            if (ownTransaction && !saved)
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    // Applies the context to its tracked entity and writes it; the caller's transaction commits it.
    private async Task<ProcessorContext> SaveTrackedAsync(
        ProcessorContext context,
        CancellationToken cancellationToken)
    {
        var entity = await LoadOrAddEntityAsync(context.ProcessId, cancellationToken);
        var storedVersion = dbContext.Entry(entity).State == EntityState.Added ? 0 : entity.Version;

        if (storedVersion != context.Version)
        {
            throw new KaleidoConcurrencyException(context.ProcessId);
        }

        entity.Version = context.Version + 1;
        entity.LatestRequestId = context.LatestRequestId;
        entity.CreatedByName = context.CreatedBy?.Name;
        entity.CreatedById = context.CreatedBy?.Id;
        entity.UpdatedByName = context.UpdatedBy?.Name;
        entity.UpdatedById = context.UpdatedBy?.Id;
        entity.State = context.State;
        entity.TargetProcessorName = context.TargetProcessorName;
        entity.CreatedUtc = context.CreatedUtc == default ? DateTimeOffset.UtcNow : context.CreatedUtc;
        entity.UpdatedUtc = context.UpdatedUtc == default ? DateTimeOffset.UtcNow : context.UpdatedUtc;

        // A new process's row is inserted on its own first: a primary-key violation of that statement
        // can only be the process id, so a process created concurrently is told apart from any other
        // failure without reading the message (#513).
        if (dbContext.Entry(entity).State == EntityState.Added)
        {
            await InsertProcessAsync(context, cancellationToken);
        }

        SyncSteps(entity, context);
        SyncAvailableSteps(entity, context);
        SyncRequiredStep(entity, context);

        await SaveChangesAsync(context, cancellationToken);

        return context with { Version = entity.Version };
    }

    private async Task InsertProcessAsync(ProcessorContext context, CancellationToken cancellationToken)
    {
        const int SqliteConstraintPrimaryKey = 1555;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintPrimaryKey })
        {
            throw new KaleidoConcurrencyException(context.ProcessId, exception);
        }
    }

    // Another instance saved the row since it was read (the version no longer matches): this save lost
    // the race. Any other failure (a duplicate outbox event, another constraint, a missing table) is a
    // store failure, not a conflict.
    private async Task SaveChangesAsync(ProcessorContext context, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new KaleidoConcurrencyException(context.ProcessId, exception);
        }
    }

    // The tracked entity with its children, or a new one added to the context.
    private async Task<ProcessorContextEntity> LoadOrAddEntityAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        var entity =
            await dbContext.ProcessContexts
                .Include(x => x.Steps)
                .Include(x => x.AvailableSteps)
                .Include(x => x.RequiredStep)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    x => x.ProcessId ==
                         processId,
                    cancellationToken);

        if (entity is null)
        {
            entity = new ProcessorContextEntity { ProcessId = processId };
            dbContext.ProcessContexts.Add(entity);
        }

        return entity;
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

            row.Status = step.Status;
            row.LatestRequestId = step.LatestRequestId;
            row.FirstExecuted = step.FirstExecuted;
            row.StartedByName = step.StartedBy?.Name;
            row.StartedById = step.StartedBy?.Id;
            row.LastExecuted = step.LastExecuted;
            row.LastUpdatedByName = step.LastUpdatedBy?.Name;
            row.LastUpdatedById = step.LastUpdatedBy?.Id;
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
                    Status = step.Status,
                    LatestRequestId = step.LatestRequestId,
                    FirstExecuted = step.FirstExecuted,
                    StartedByName = step.StartedBy?.Name,
                    StartedById = step.StartedBy?.Id,
                    LastExecuted = step.LastExecuted,
                    LastUpdatedByName = step.LastUpdatedBy?.Name,
                    LastUpdatedById = step.LastUpdatedBy?.Id,
                });
        }
    }

    private static void SyncAvailableSteps(
        ProcessorContextEntity entity,
        ProcessorContext context)
    {
        var incoming = context.AvailableSteps
            .ToHashSet(StringComparer.Ordinal);

        foreach (var row in entity.AvailableSteps.Where(x => !incoming.Contains(x.StepName)).ToArray())
        {
            entity.AvailableSteps.Remove(row);
        }

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
                    Sequence = index,
                });
        }
    }

    private static void SyncRequiredStep(
        ProcessorContextEntity entity,
        ProcessorContext context)
    {
        if (context.RequiredStep is null)
        {
            if (entity.RequiredStep is not null)
            {
                entity.RequiredStep = null;
            }

            return;
        }

        var informationRequestJson =
            context.RequiredInformationRequest is null
                ? null
                : JsonSerializer.Serialize(context.RequiredInformationRequest, JsonOptions);

        if (entity.RequiredStep is not null)
        {
            entity.RequiredStep.StepName = context.RequiredStep;
            entity.RequiredStep.InformationRequestJson = informationRequestJson;
            return;
        }

        entity.RequiredStep = new ProcessorRequiredStepEntity
        {
            ProcessId = context.ProcessId,
            StepName = context.RequiredStep,
            InformationRequestJson = informationRequestJson,
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static ProcessorContext ToProcessorContext(
        ProcessorContextEntity entity,
        string localProcessorName)
    {
        return new ProcessorContext
        {
            ProcessId = entity.ProcessId,
            ProcessorName = localProcessorName,
            Version = entity.Version,
            LatestRequestId = entity.LatestRequestId,
            CreatedBy = Caller(entity.CreatedByName, entity.CreatedById),
            UpdatedBy = Caller(entity.UpdatedByName, entity.UpdatedById),
            State = entity.State,
            RequiredStep = entity.RequiredStep?.StepName,
            RequiredInformationRequest =
                entity.RequiredStep?.InformationRequestJson is { } json
                    ? JsonSerializer.Deserialize<InformationRequest>(json, JsonOptions)
                    : null,
            TargetProcessorName = entity.TargetProcessorName,

            CreatedUtc = entity.CreatedUtc,
            UpdatedUtc = entity.UpdatedUtc,

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
                            StepName = step.StepName,
                            Status = step.Status,
                            LatestRequestId = step.LatestRequestId,
                            FirstExecuted = step.FirstExecuted,
                            StartedBy = Caller(step.StartedByName, step.StartedById),
                            LastExecuted = step.LastExecuted,
                            LastUpdatedBy = Caller(step.LastUpdatedByName, step.LastUpdatedById),
                        })
                    .ToArray(),
        };
    }

    private static KaleidoCaller? Caller(string? name, string? id) =>
        name is null && id is null ? null : new KaleidoCaller { Name = name, Id = id };

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load process context for process {ProcessId}.")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception, Guid processId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process context saved for process {ProcessId} ({StepCount} steps).")]
    private static partial void LogSaved(ILogger logger, Guid processId, int stepCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save process context for process {ProcessId}.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, Guid processId);
}
