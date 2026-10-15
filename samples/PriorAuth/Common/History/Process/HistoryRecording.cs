using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.History.Process;

/// <summary>
/// Runs a step whose changes must reach both the processor's own database and History as one unit:
/// the step runs in a database transaction that commits only when the step succeeds. A step that is
/// rejected or fails leaves nothing behind; when History does not record the change, the step's
/// changes are rolled back and it is rejected with the result <c>notRecorded</c> builds from the
/// <see cref="HistoryNotRecordedException"/> (already logged by the History client).
/// </summary>
/// <remarks>
/// The one gap left: History records and then the local commit fails. History is then ahead, not
/// missing anything, and retrying the step records the same snapshot again.
/// </remarks>
public static class HistoryRecording
{
    public static async Task<TResult> InTransactionAsync<TResult>(
        DbContext dbContext,
        Func<Task<TResult>> step,
        Func<HistoryNotRecordedException, TResult> notRecorded,
        CancellationToken cancellationToken)
        where TResult : IProcessStepHandlerResult
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        TResult result;

        try
        {
            result = await step();
        }
        catch (HistoryNotRecordedException exception)
        {
            return notRecorded(exception);
        }

        if (result.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }
}
