using System.Text.Json;
using System.Text.Json.Serialization;
using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Kaleido.Provider.SQLite.Entities;
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SqliteTelemetry.LoadFailuresCounter.Add(1);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            LogLoadFailed(logger, exception, processId);
            throw;
        }
    }

    public async Task<ProcessorContext> SaveAsync(
        ProcessorContext context,
        IReadOnlyCollection<KaleidoOutboxEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            context);
        ArgumentNullException.ThrowIfNull(
            events);

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = SqliteTelemetry.ActivitySource
            .StartActivity(SqliteTelemetry.SaveActivityName, ActivityKind.Internal);
        activity?.SetTag(SqliteTelemetry.TagProcessId, context.ProcessId.ToString());

        try
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(
                    cancellationToken);

            var entity = await LoadOrAddEntityAsync(context.ProcessId, cancellationToken);
            var storedVersion = dbContext.Entry(entity).State == EntityState.Added ? 0 : entity.Version;

            if (storedVersion != context.Version)
            {
                throw new KaleidoConcurrencyException(context.ProcessId);
            }

            entity.Version = context.Version + 1;
            entity.LatestRequestId = context.LatestRequestId;
            entity.Owner = context.Owner;
            entity.State = context.State;
            entity.CreatedUtc = context.CreatedUtc == default ? DateTimeOffset.UtcNow : context.CreatedUtc;
            entity.UpdatedUtc = context.UpdatedUtc == default ? DateTimeOffset.UtcNow : context.UpdatedUtc;

            SyncSteps(entity, context);
            SyncAvailableSteps(entity, context);
            SyncRequiredStep(entity, context, serviceOptions.ServiceName);

            // Same transaction: the events are saved only together with the state.
            dbContext.OutboxEvents.AddRange(events.Select(ToEntity));

            await SaveChangesAsync(context, cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            LogSaved(logger, context.ProcessId, context.Steps.Count);

            return context with { Version = entity.Version };
        }
        // A conflict is not a store failure: the request lost the race and the caller rereads (409).
        catch (Exception exception) when (exception is not OperationCanceledException and not KaleidoConcurrencyException)
        {
            SqliteTelemetry.SaveFailuresCounter.Add(1);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            LogSaveFailed(logger, exception, context.ProcessId);
            throw;
        }
    }

    public async Task<IdempotentResult?> FindResultAsync(string key, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.IdempotentResults
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        return entity is null || entity.ExpiresUtc <= DateTimeOffset.UtcNow
            ? null
            : new IdempotentResult
            {
                Key = entity.Key,
                RequestHash = entity.RequestHash,
                StatusCode = entity.StatusCode,
                Response = entity.Response,
                ExpiresUtc = entity.ExpiresUtc,
            };
    }

    public async Task SaveResultAsync(IdempotentResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Expired answers are deleted as new ones arrive, so the table does not grow without bound.
        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM IdempotentResults WHERE ExpiresUtc <= {nowTicks}",
            cancellationToken);

        var entity = await dbContext.IdempotentResults.FirstOrDefaultAsync(x => x.Key == result.Key, cancellationToken);

        if (entity is null)
        {
            entity = new IdempotentResultEntity { Key = result.Key };
            dbContext.IdempotentResults.Add(entity);
        }

        entity.RequestHash = result.RequestHash;
        entity.StatusCode = result.StatusCode;
        entity.Response = result.Response;
        entity.ExpiresUtc = result.ExpiresUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnqueueEventsAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        dbContext.OutboxEvents.AddRange(events.Select(ToEntity));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Each statement is atomic in SQLite (writers are serialized): the UPDATE re-checks that an entry
    // is still unleased, so two instances claiming at once never lease the same entry, and each
    // reads back only the entries it leased itself.
    public async Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var leaseUntil = now + lease.Ticks;

        var due = await dbContext.OutboxEvents
            .AsNoTracking()
            .Where(x => !x.IsParked && x.NextAttemptUtc <= now && x.LeaseUntil <= now)
            .OrderBy(x => x.OccurredUtc)
            .Select(x => x.EventId)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return [];
        }

        await dbContext.OutboxEvents
            .Where(x => due.Contains(x.EventId) && !x.IsParked && x.LeaseUntil <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.LeaseOwner, Owner)
                    .SetProperty(x => x.LeaseUntil, leaseUntil),
                cancellationToken);

        var claimed = await dbContext.OutboxEvents
            .AsNoTracking()
            .Where(x => due.Contains(x.EventId) && x.LeaseOwner == Owner && x.LeaseUntil == leaseUntil)
            .OrderBy(x => x.OccurredUtc)
            .ToListAsync(cancellationToken);

        return [.. claimed.Select(ToOutboxEvent)];
    }

    // Only while this instance still holds the lease.
    public async Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;

        await dbContext.OutboxEvents
            .Where(x => x.EventId == eventId && x.LeaseOwner == Owner && x.LeaseUntil > now)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // Only while this instance still holds the lease; releases it.
    public async Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var lastError = errorMessage.Length > MaxErrorLength ? errorMessage[..MaxErrorLength] : errorMessage;
        var parked = retryAtUtc is null;
        var retryAt = retryAtUtc?.UtcTicks ?? now;

        await dbContext.OutboxEvents
            .Where(x => x.EventId == eventId && x.LeaseOwner == Owner && x.LeaseUntil > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.LastError, lastError)
                    .SetProperty(x => x.NextAttemptUtc, retryAt)
                    .SetProperty(x => x.IsParked, parked)
                    .SetProperty(x => x.LeaseOwner, (string?)null)
                    .SetProperty(x => x.LeaseUntil, 0L),
                cancellationToken);
    }

    internal const int MaxErrorLength = 2000;

    // This instance's identity as lease owner.
    private string Owner => serviceOptions.InstanceId.ToString("N");

    private static OutboxEventEntity ToEntity(KaleidoOutboxEvent @event) =>
        new()
        {
            EventId = @event.EventId,
            EventType = @event.EventType,
            Payload = @event.Payload,
            OccurredUtc = @event.OccurredUtc.UtcTicks,
            Attempts = @event.Attempts,
            NextAttemptUtc = @event.NextAttemptUtc.UtcTicks,
            LastError = @event.LastError,
            IsParked = @event.IsParked,
        };

    private static KaleidoOutboxEvent ToOutboxEvent(OutboxEventEntity entity) =>
        new()
        {
            EventId = entity.EventId,
            EventType = entity.EventType,
            Payload = entity.Payload,
            OccurredUtc = new DateTimeOffset(entity.OccurredUtc, TimeSpan.Zero),
            Attempts = entity.Attempts,
            NextAttemptUtc = new DateTimeOffset(entity.NextAttemptUtc, TimeSpan.Zero),
            LastError = entity.LastError,
            IsParked = entity.IsParked,
        };

    // Another instance saved the row since it was read (the version no longer matches), or created
    // the same process at the same time (duplicate key): either way this save lost the race.
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
        catch (DbUpdateException exception) when (context.Version == 0)
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
                    LastExecuted = step.LastExecuted,
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

        var informationRequestJson =
            context.RequiredInformationRequest is null
                ? null
                : JsonSerializer.Serialize(context.RequiredInformationRequest, JsonOptions);

        if (entity.RequiredStep is not null)
        {
            entity.RequiredStep.ProcessorName = processorName;
            entity.RequiredStep.StepName = context.RequiredStep;
            entity.RequiredStep.InformationRequestJson = informationRequestJson;
            return;
        }

        entity.RequiredStep = new ProcessorRequiredStepEntity
        {
            ProcessId = context.ProcessId,
            ProcessorName = processorName,
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
            Owner = entity.Owner,
            State = entity.State,
            RequiredStep = entity.RequiredStep?.StepName,
            RequiredInformationRequest =
                entity.RequiredStep?.InformationRequestJson is { } json
                    ? JsonSerializer.Deserialize<InformationRequest>(json, JsonOptions)
                    : null,

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
                            Version = step.Version,
                            Status = step.Status,
                            LatestRequestId = step.LatestRequestId,
                            LastExecuted = step.LastExecuted,
                        })
                    .ToArray(),
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load process context for process {ProcessId}.")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception, Guid processId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process context saved for process {ProcessId} ({StepCount} steps).")]
    private static partial void LogSaved(ILogger logger, Guid processId, int stepCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save process context for process {ProcessId}.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, Guid processId);
}
