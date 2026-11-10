namespace Kaleido.Processor.Execution;

/// <summary>
/// The answer of the service a process is handed off to, as the handing-off step passes it on with
/// <see cref="ProcessStepHandlerResult.HandOff(string, IHandOffContinuationSource)"/>: whether the
/// target's step succeeded, what it said, and what comes next there (#198).
/// </summary>
/// <remarks>
/// <para>
/// On the request that hands off, the runtime answers with it: the process's required step, its
/// questions and its available steps are the target's, by name, and the handing-off step's
/// business and framework messages are the target's. A transport resolves the names against the target's published
/// registry, so a consumer continues on the target with no second call.
/// </para>
/// <para>
/// It is never saved: the process state keeps only the target's name, and a later request reads
/// the target's own state.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record HandOffContinuation
{
    /// <summary>The service that takes the process over.</summary>
    public required string TargetProcessorName { get; init; }

    /// <summary><see langword="true"/> when the target's step completed.</summary>
    public bool Succeeded { get; init; }

    /// <summary><see langword="true"/> when the target rejected its step's input as correctable.</summary>
    public bool Rejected { get; init; }

    /// <summary>
    /// The target step's business messages, returned to the caller as the handing-off step's business
    /// messages.
    /// </summary>
    public IReadOnlyCollection<ProcessMessage> BusinessMessages { get; init; }
        = [];

    /// <summary>
    /// The target's framework messages about its step (diagnostics, sent only when the target includes
    /// them), carried as the handing-off step's runtime messages: the origin's transport options decide
    /// whether its caller sees them, as for its own.
    /// </summary>
    public IReadOnlyCollection<ProcessMessage> FrameworkMessages { get; init; }
        = [];

    /// <summary>The name of the step the target requires next, or <see langword="null"/>.</summary>
    public string? RequiredStep { get; init; }

    /// <summary>
    /// The questions <see cref="RequiredStep"/> needs answered, with the target's exchange id, or
    /// <see langword="null"/> for an ordinary step.
    /// </summary>
    public InformationRequest? InformationRequest { get; init; }

    /// <summary>The names of the steps the caller may run next on the target.</summary>
    public IReadOnlyCollection<string> AvailableSteps { get; init; }
        = [];

    /// <summary>
    /// The target's notice, unchanged: for example <c>pro_not_permitted</c> when it left out steps the
    /// caller may not run.
    /// </summary>
    public KaleidoNotice? Notice { get; init; }
}
