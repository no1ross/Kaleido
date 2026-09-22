namespace Kaleido.Process.Execution;

public interface IProcessStepHandlerResult
{
    bool Succeeded { get; }

    string? RequiredStep { get; }

    string? TargetProcessorName { get; }

    object? Response { get; }

    IReadOnlyCollection<ProcessMessage> Messages { get; }
}

public sealed record ProcessStepHandlerResult<TProcessStepResult> : IProcessStepHandlerResult
{
    internal ProcessStepHandlerResult() { }

    public bool Succeeded { get; init; }

    public string? RequiredStep { get; init; }

    public string? TargetProcessorName { get; init; }

    public required TProcessStepResult Response { get; init; }

    object? IProcessStepHandlerResult.Response => Response;

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];

    public static ProcessStepHandlerResult<TProcessStepResult> Success(
        TProcessStepResult response,
        string? requiredStep = null,
        string? targetProcessorName = null,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            RequiredStep = requiredStep,
            TargetProcessorName = targetProcessorName,
            Messages = messages,
            Response = response
        };
    }

    public static ProcessStepHandlerResult<TProcessStepResult> Failure(
        TProcessStepResult response,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = false,
            Messages = messages,
            Response = response
        };
    }

    /// <summary>
    /// Signals a successful step that hands off to a different processor.
    /// The framework will propagate <paramref name="targetProcessorName"/> to the
    /// HTTP response so the consumer can fetch authoritative state from the target.
    /// </summary>
    public static ProcessStepHandlerResult HandOff(
        string targetProcessorName,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            TargetProcessorName = targetProcessorName,
            Messages = messages
        };
    }
}

public record ProcessStepHandlerResult
    : IProcessStepHandlerResult
{
    internal ProcessStepHandlerResult() { }

    public bool Succeeded { get; init; }

    public string? RequiredStep { get; init; }

    public string? TargetProcessorName { get; init; }

    public object? Response { get; init; }

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];

    public static ProcessStepHandlerResult Success(
        string? requiredStep = null,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            RequiredStep = requiredStep,
            Messages = messages
        };
    }

    public static ProcessStepHandlerResult Success(
        params ProcessMessage[] messages)
    {
        return Success(null, messages);
    }

    public static ProcessStepHandlerResult Failure(
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = false,
            Messages = messages
        };
    }

    /// <summary>
    /// Signals a successful step that hands off to a different processor.
    /// The framework will propagate <paramref name="targetProcessorName"/> to the
    /// HTTP response so the consumer can fetch authoritative state from the target.
    /// </summary>
    public static ProcessStepHandlerResult HandOff(
        string targetProcessorName,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            TargetProcessorName = targetProcessorName,
            Messages = messages
        };
    }
}
