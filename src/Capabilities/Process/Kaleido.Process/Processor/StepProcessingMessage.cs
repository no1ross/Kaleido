namespace Kaleido.Processor;

/// <summary>
/// A message from the Process runtime about one submitted step: a planning or validation result,
/// or a problem with its execution. Distinct from the business messages a step's handler returns
/// (<see cref="ProcessMessage"/>).
/// </summary>
/// <remarks>
/// Planning messages explain why a step was or was not put in the execution plan; execution
/// messages report a process violation, a validation failure thrown by the handler, an unexpected
/// error, or a cancellation.
/// </remarks>
public sealed record StepProcessingMessage
{
    /// <summary>
    /// The message's severity. During planning, an <see cref="MessageType.Error"/> keeps the step
    /// out of the execution plan.
    /// </summary>
    public required MessageType Type { get; init; }

    /// <summary>
    /// The machine-readable code: a <see cref="ProcessorErrorCodes"/> value, or the code of a
    /// <see cref="KaleidoValidationException"/> thrown by the step's handler.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>The human-readable explanation.</summary>
    public required string Message { get; init; }

    /// <summary>Creates a <see cref="MessageType.Information"/> message.</summary>
    /// <param name="code">The machine-readable code.</param>
    /// <param name="message">The human-readable explanation.</param>
    /// <returns>The message.</returns>
    public static StepProcessingMessage Information(
        string code,
        string message)
    {
        return new()
        {
            Type = MessageType.Information,
            Code = code,
            Message = message,
        };
    }

    /// <summary>Creates a <see cref="MessageType.Warning"/> message.</summary>
    /// <param name="code">The machine-readable code.</param>
    /// <param name="message">The human-readable explanation.</param>
    /// <returns>The message.</returns>
    public static StepProcessingMessage Warning(
        string code,
        string message)
    {
        return new()
        {
            Type = MessageType.Warning,
            Code = code,
            Message = message,
        };
    }

    /// <summary>Creates a <see cref="MessageType.Error"/> message.</summary>
    /// <param name="code">The machine-readable code.</param>
    /// <param name="message">The human-readable explanation.</param>
    /// <returns>The message.</returns>
    public static StepProcessingMessage Error(
        string code,
        string message)
    {
        return new()
        {
            Type = MessageType.Error,
            Code = code,
            Message = message,
        };
    }
}
