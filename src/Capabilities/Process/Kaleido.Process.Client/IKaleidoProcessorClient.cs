namespace Kaleido.Process.Client;

/// <summary>
/// Invokes another service's Process capability without choosing a transport or accessing its local
/// step catalog. The factory selects the remote service and version; inputs and results remain neutral.
/// </summary>
/// <remarks>
/// A remote step can be represented by a local mirror record with the same published step name,
/// without implementing <see cref="IProcessStep"/> or registering it as a local step. A transport
/// adapter resolves callable operations from the participant's hydrated, unfiltered Registry and
/// reports remote failures in that transport's terms.
/// </remarks>
public interface IKaleidoProcessorClient
{
    /// <summary>Reads current durable state without running a step.</summary>
    /// <param name="processId">The existing remote process id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe state snapshot, or <see langword="null"/> when that process does not exist.</returns>
    Task<ProcessStateSnapshot?> GetProcessStateAsync(Guid processId, CancellationToken cancellationToken);

    /// <summary>Submits one or more typed steps in the same neutral Process request shape as local execution.</summary>
    /// <param name="request">The process id, when continuing, and typed steps keyed by their published names.</param>
    /// <param name="idempotencyKey">The stable identity of this logical call; null lets an adapter choose a new one.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The process id, state, next-step names and submitted-step outcomes.</returns>
    /// <remarks>A local mirror record can be placed directly in <see cref="ProcessorRequest.Steps"/> under the remote step name. A new logical call needs a new key; retrying the same call uses the same request and key.</remarks>
    Task<ProcessResult> ExecuteAsync(ProcessRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Submits a typed step under its CLR type name to the remote service's matching step.</summary>
    /// <typeparam name="TStep">The remote step's type, or a local mirror with the same type name.</typeparam>
    /// <param name="processStep">The typed step input.</param>
    /// <param name="processId">The existing process id to continue, or null to start a new one.</param>
    /// <param name="idempotencyKey">The stable identity of this logical call; null chooses a new one.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The existing neutral Process result, including that step's outcome.</returns>
    Task<ProcessResult> ExecuteStepAsync<TStep>(TStep processStep, Guid? processId, string? idempotencyKey, CancellationToken cancellationToken)
        where TStep : class;

    /// <summary>Submits a step by its published name when the caller has no compile-time mirror type.</summary>
    /// <param name="stepName">The remote step's published name.</param>
    /// <param name="processStep">The input object for that step, interpreted by the chosen transport.</param>
    /// <param name="processId">The existing process id to continue, or null to start a new one.</param>
    /// <param name="idempotencyKey">The stable identity of this logical call; null chooses a new one.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The existing neutral Process result, not a transport-specific step response.</returns>
    Task<ProcessResult> ExecuteStepAsync(string stepName, object processStep, Guid? processId, string? idempotencyKey, CancellationToken cancellationToken);
}
