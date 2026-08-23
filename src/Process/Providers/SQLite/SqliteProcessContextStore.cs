using Kaleido.Process.Participant.Context;
using Kaleido.Process.Providers.SQLite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Process.Providers.SQLite;

internal sealed class SqliteProcessContextStore(
    SqliteProcessContextDbContext dbContext)
    : IProcessContextStore
{
    public async Task<ParticipantContext?> LoadAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity =
            await dbContext.ProcessContexts
                .AsNoTracking()
                .Include(x => x.Steps)
                .Include(x => x.AvailableSteps)
                .FirstOrDefaultAsync(
                    x => x.ProcessId ==
                         processId,
                    cancellationToken);

        if (entity is null)
        {
            return new ParticipantContext
            {
                ProcessId =
                    processId
            };
        }

        return ToParticipantContext(
            entity);
    }

    public async Task SaveAsync(
        ParticipantContext context,
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
        }

        entity.LatestRequestId =
            context.LatestRequestId;

        entity.State =
            context.State;

        entity.RequiredStep =
            context.RequiredStep;

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

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    private static ParticipantContext ToParticipantContext(
        ProcessContextEntity entity)
    {
        return new ParticipantContext
        {
            ProcessId =
                entity.ProcessId,

            LatestRequestId =
                entity.LatestRequestId,

            State =
                entity.State,

            RequiredStep =
                entity.RequiredStep,

            CreatedUtc =
                entity.CreatedUtc,

            UpdatedUtc =
                entity.UpdatedUtc,

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