namespace Kaleido.Process.Exceptions;

/// <summary>
/// Thrown when a process was changed by another request since this one loaded it: the save was
/// refused (optimistic concurrency) and nothing of this request's change was stored. Its code is
/// <see cref="Kaleido.Process.ProcessorErrorCodes.ProcessConflict"/>; the adapter chooses the response.
/// </summary>
/// <remarks>
/// The caller reads the process state again and decides on the next step; retrying the same request
/// blindly would act on a state it has not seen. Thrown by <see cref="Kaleido.Process.Storage.IProcessContextStore"/>
/// implementations when the stored version is not the version the context was loaded with.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed class ProcessConcurrencyException : Exception
{
    /// <summary>Creates the exception for the process that was changed concurrently.</summary>
    /// <param name="processId">The process whose save was refused.</param>
    public ProcessConcurrencyException(Guid processId)
        : base(Describe(processId))
    {
        ProcessId = processId;
    }

    /// <summary>Creates the exception for the process that was changed concurrently, with the store's own error.</summary>
    /// <param name="processId">The process whose save was refused.</param>
    /// <param name="innerException">The store's error that reported the conflict.</param>
    public ProcessConcurrencyException(Guid processId, Exception innerException)
        : base(Describe(processId), innerException)
    {
        ProcessId = processId;
    }

    /// <summary>The process whose save was refused.</summary>
    public Guid ProcessId { get; }

    /// <summary>Stable machine-readable error code: <see cref="Kaleido.Process.ProcessorErrorCodes.ProcessConflict"/>.</summary>
    public string Code => Kaleido.Process.ProcessorErrorCodes.ProcessConflict;

    private static string Describe(Guid processId) =>
        $"Process '{processId}' was changed by another request. Read its state again and decide on the next step.";
}
