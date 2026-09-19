using Kaleido.Process.Context;
using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

internal sealed class SqliteProcessContextStore(
    SqliteProcessContextDbContext dbContext,
    KaleidoServiceOptions serviceOptions)
    : IProcessContextStore
{
    public async Task<ProcessorContext?> LoadAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

    public async Task SaveAsync(
        ProcessorContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            context);

        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var entity =
            await dbContext.ProcessContexts
                .FirstOrDefaultAsync(
                    x => x.ProcessId ==
                         context.ProcessId,
                    cancellationToken);

        if (entity is null)
        {
            entity =
                new ProcessContextEntity
                {
                    ProcessId =
                        context.ProcessId
                };

            dbContext.ProcessContexts.Add(
                entity);
        }
        else
        {
            await dbContext.ProcessStepContexts
                .Where(x =>
                    x.ProcessId ==
                    context.ProcessId)
                .ExecuteDeleteAsync(
                    cancellationToken);

            await dbContext.ProcessAvailableSteps
                .Where(x =>
                    x.ProcessId ==
                    context.ProcessId)
                .ExecuteDeleteAsync(
                    cancellationToken);

            await dbContext.ProcessRequiredSteps
                .Where(x =>
                    x.ProcessId ==
                    context.ProcessId)
                .ExecuteDeleteAsync(
                    cancellationToken);
        }

        entity.LatestRequestId =
            context.LatestRequestId;

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

        var stepEntities =
            context.Steps
                .Select(step =>
                    new ProcessStepContextEntity
                    {
                        ProcessId =
                            context.ProcessId,

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
                .ToArray();

        // Available steps are always local — store only the step name.
        var availableStepEntities =
            context.AvailableSteps
                .Select(
                    (stepName, index) =>
                        new ProcessAvailableStepEntity
                        {
                            ProcessId =
                                context.ProcessId,

                            StepName =
                                stepName,

                            Sequence =
                                index
                        })
                .ToArray();

        dbContext.ProcessStepContexts.AddRange(
            stepEntities);

        dbContext.ProcessAvailableSteps.AddRange(
            availableStepEntities);

        if (context.RequiredStep is not null)
        {
            var localProcessorName =
                serviceOptions.ServiceName;

            dbContext.ProcessRequiredSteps.Add(
                new ProcessRequiredStepEntity
                {
                    ProcessId =
                        context.ProcessId,

                    // Store the target processor name when cross-processor,
                    // otherwise store the local processor name for backwards compatibility.
                    ProcessorName =
                        context.TargetProcessorName ?? localProcessorName,

                    StepName =
                        context.RequiredStep
                });
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    private static ProcessorContext ToProcessorContext(
        ProcessContextEntity entity,
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
                    .OrderBy(x => x.StepName)
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
