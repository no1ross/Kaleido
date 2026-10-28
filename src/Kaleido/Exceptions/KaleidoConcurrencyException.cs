namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when a process was changed by another request since this one loaded it: the save was
/// refused (optimistic concurrency) and nothing of this request's change was stored. Results in a
/// 409 Conflict with <see cref="Kaleido.Processor.ProcessorErrorCodes.ProcessConflict"/>.
/// </summary>
/// <remarks>
/// The caller reads the process state again and decides on the next step; retrying the same request
/// blindly would act on a state it has not seen. Thrown by <see cref="Kaleido.Storage.IKaleidoContextStore"/>
/// implementations when the stored version is not the version the context was loaded with.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed class KaleidoConcurrencyException : Exception
{
    /// <summary>Creates the exception for the process that was changed concurrently.</summary>
    /// <param name="processId">The process whose save was refused.</param>
    public KaleidoConcurrencyException(Guid processId)
        : base(Describe(processId))
    {
        ProcessId = processId;
    }

    /// <summary>Creates the exception for the process that was changed concurrently, with the store's own error.</summary>
    /// <param name="processId">The process whose save was refused.</param>
    /// <param name="innerException">The store's error that reported the conflict.</param>
    public KaleidoConcurrencyException(Guid processId, Exception innerException)
        : base(Describe(processId), innerException)
    {
        ProcessId = processId;
    }

    /// <summary>The process whose save was refused.</summary>
    public Guid ProcessId { get; }

    /// <summary>Stable machine-readable error code: <see cref="Kaleido.Processor.ProcessorErrorCodes.ProcessConflict"/>.</summary>
    public string Code => Kaleido.Processor.ProcessorErrorCodes.ProcessConflict;

    private static string Describe(Guid processId) =>
        $"Process '{processId}' was changed by another request. Read its state again and decide on the next step.";
}
