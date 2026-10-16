namespace Kaleido.Processor;

/// <summary>
/// A business message a step's handler returns with its result, passed through to the caller as
/// is. Distinct from the runtime's own messages about the step (<see cref="StepProcessingMessage"/>).
/// </summary>
/// <remarks>
/// The code, severity and wording are the implementer's; the runtime does not interpret them.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ProcessMessage
{
    /// <summary>The machine-readable code, defined by the implementer.</summary>
    public required string Code { get; init; }

    /// <summary>The message's severity, as set by the handler.</summary>
    public required MessageType Type { get; init; }

    /// <summary>The human-readable explanation.</summary>
    public required string Message { get; init; }
}
