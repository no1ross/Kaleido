using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Providers.SQLite.Entities;
using Kaleido.Process.Registry;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Process.Providers.SQLite;

internal sealed class SqliteProcessContextStore(
    SqliteProcessContextDbContext dbContext,
    IProcessorRegistry processorRegistry)
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

        var localProcessorName =
            processorRegistry.Registrations
                .Single()
                .Name;

        if (entity is null)
        {
            return new ProcessorContext
            {
                ProcessId = processId,
                ProcessorName = localProcessorName
            };
        }

        return ToProcessorContext(
            entity,
            localProcessorName);
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
                    (reference, index) =>
                        new ProcessAvailableStepEntity
                        {
                            ProcessId =
                                context.ProcessId,

                            StepName =
                                reference.StepName,

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
            dbContext.ProcessRequiredSteps.Add(
                new ProcessRequiredStepEntity
                {
                    ProcessId =
                        context.ProcessId,

                    ProcessorName =
                        context.RequiredStep.ProcessorName,

                    StepName =
                        context.RequiredStep.StepName
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
                entity.RequiredStep is null
                    ? null
                    : new ProcessStepReference
                    {
                        ProcessorName =
                            entity.RequiredStep.ProcessorName,

                        StepName =
                            entity.RequiredStep.StepName
                    },

            CreatedUtc =
                entity.CreatedUtc,

            UpdatedUtc =
                entity.UpdatedUtc,

            // Available steps are always local — reconstruct references
            // using the current processor name.
            AvailableSteps =
                entity.AvailableSteps
                    .OrderBy(x => x.Sequence)
                    .Select(x =>
                        new ProcessStepReference
                        {
                            ProcessorName = localProcessorName,
                            StepName = x.StepName
                        })
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
