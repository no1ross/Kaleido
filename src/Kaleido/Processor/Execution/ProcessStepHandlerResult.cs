namespace Kaleido.Processor.Execution;

/// <summary>
/// The outcome a step handler returns to the runtime.
/// </summary>
/// <remarks>
/// Create instances through the factory methods on <see cref="ProcessStepHandlerResult"/>;
/// with a typed response, the response type is inferred and the next step is added with
/// <see cref="ProcessStepHandlerResult{TProcessStepResult}.Next{TNext}"/> or
/// <see cref="ProcessStepHandlerResult{TProcessStepResult}.RequireInformation{TNext}"/>. The next
/// step is always identified by type; its public name is resolved by the runtime only when
/// state, events, and responses are written.
/// </remarks>
public interface IProcessStepHandlerResult
{
    /// <summary>
    /// <see langword="true"/> when the step succeeded; <see langword="false"/> for a
    /// business failure or a rejection.
    /// </summary>
    bool Succeeded { get; }

    /// <summary>
    /// <see langword="true"/> when the handler rejected the step's input as correctable: the step
    /// is not completed and the process is left exactly as it was (see <c>Rejected(...)</c>).
    /// </summary>
    bool Rejected { get; }

    /// <summary>
    /// The <see cref="IProcessStep"/> type that must execute next, or
    /// <see langword="null"/> when the handler does not require a specific step.
    /// </summary>
    Type? RequiredStep { get; }

    /// <summary>
    /// The questions <see cref="RequiredStep"/> needs answered, or <see langword="null"/>.
    /// Only set together with a required <see cref="IInformationStep"/>, through
    /// <c>RequireInformation&lt;TNext&gt;</c>.
    /// </summary>
    InformationRequest? InformationRequest { get; }

    /// <summary>
    /// The processor this step hands off to, or <see langword="null"/> when the
    /// process stays in the current processor.
    /// </summary>
    string? TargetProcessorName { get; }

    /// <summary>
    /// <see langword="true"/> when the step completes the whole process (<c>Complete(...)</c>): the
    /// process becomes complete whatever steps its rules still offer, and no further step runs.
    /// </summary>
    bool CompletesProcess { get; }

    /// <summary>
    /// The step's response payload, if any.
    /// </summary>
    object? Response { get; }

    /// <summary>
    /// Business messages returned to the caller.
    /// </summary>
    IReadOnlyCollection<ProcessMessage> Messages { get; }
}

/// <summary>
/// The outcome of a step handler that returns no typed response.
/// </summary>
public sealed record ProcessStepHandlerResult : IProcessStepHandlerResult
{
    internal ProcessStepHandlerResult() { }

    /// <inheritdoc />
    public bool Succeeded { get; internal init; }

    /// <inheritdoc />
    public bool Rejected { get; internal init; }

    /// <inheritdoc />
    public Type? RequiredStep { get; internal init; }

    /// <inheritdoc />
    public InformationRequest? InformationRequest { get; internal init; }

    /// <inheritdoc />
    public string? TargetProcessorName { get; internal init; }

    /// <inheritdoc />
    public bool CompletesProcess { get; internal init; }

    /// <inheritdoc />
    public object? Response { get; internal init; }

    /// <inheritdoc />
    public IReadOnlyCollection<ProcessMessage> Messages { get; internal init; }
        = [];

    /// <summary>
    /// Signals a successful step with no specific next step required; the runtime
    /// continues with the steps the process rules make available.
    /// </summary>
    /// <param name="messages">Business messages returned to the caller.</param>
    public static ProcessStepHandlerResult Success(
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            Messages = messages,
        };
    }

    /// <summary>
    /// Signals a successful step and requires <typeparamref name="TNext"/> to
    /// execute next.
    /// </summary>
    /// <typeparam name="TNext">
    /// The step that must execute next. It must be a legal next step under the
    /// process rules; otherwise the runtime reports a process violation.
    /// </typeparam>
    /// <param name="messages">Business messages returned to the caller.</param>
    public static ProcessStepHandlerResult Success<TNext>(
        params ProcessMessage[] messages)
        where TNext : IProcessStep
    {
        return new()
        {
            Succeeded = true,
            RequiredStep = typeof(TNext),
            Messages = messages,
        };
    }

    /// <summary>
    /// Signals a successful step that requires the information step <typeparamref name="TNext"/>
    /// next, with the questions it needs answered. The process waits in
    /// <c>AwaitingInformation</c> until the answers are submitted as <typeparamref name="TNext"/>'s payload.
    /// </summary>
    /// <typeparam name="TNext">
    /// The information step that receives the answers. It must be a legal next step under the
    /// process rules; to loop, a <see cref="RepeatableAttribute">[Repeatable]</see> information
    /// step can require itself with the next request.
    /// </typeparam>
    /// <param name="request">The questions; the content is the implementer's, the shape is Kaleido's.</param>
    /// <param name="messages">Business messages returned to the caller.</param>
    public static ProcessStepHandlerResult RequireInformation<TNext>(
        InformationRequest request,
        params ProcessMessage[] messages)
        where TNext : IInformationStep
    {
        ArgumentNullException.ThrowIfNull(request);

        return new()
        {
            Succeeded = true,
            RequiredStep = typeof(TNext),
            InformationRequest = request,
            Messages = messages,
        };
    }

    /// <summary>
    /// Signals a successful step that completes the whole process: the process becomes
    /// <c>Complete</c> whatever steps its rules still offer (an audit, an appeal), and no further step
    /// runs on it - a later step is refused with <c>pro_process_ended</c> (#534).
    /// </summary>
    /// <remarks>
    /// The end of a process is an explicit, usually internal decision: for example a job that closes a
    /// case once its appeal window has passed calls an internal-only step whose handler returns this.
    /// Such a step should run after other steps (<c>[AvailableAfter&lt;T&gt;]</c> or
    /// <c>[DependsOn&lt;T&gt;]</c>), not start a process (KAL2022).
    /// </remarks>
    /// <param name="messages">Business messages returned to the caller.</param>
    public static ProcessStepHandlerResult Complete(
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            CompletesProcess = true,
            Messages = messages,
        };
    }

    /// <summary>
    /// Signals a business failure: the request cannot proceed, and the process ends in
    /// <c>BusinessFailure</c>: no further step runs on it, so the caller starts a new process. For
    /// input the caller can correct, use <see cref="Reject"/>.
    /// </summary>
    /// <param name="messages">Business messages explaining the failure.</param>
    public static ProcessStepHandlerResult Failure(
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = false,
            Messages = messages,
        };
    }

    /// <summary>
    /// Rejects the step's input as correctable (an unknown code, a duplicate, an ineligible date):
    /// the step is not completed and the process is left exactly as it was, so the caller can
    /// correct the input and retry, or choose another available step. The handler must not have
    /// changed anything it would need to undo.
    /// </summary>
    /// <param name="messages">Business messages explaining what to correct.</param>
    public static ProcessStepHandlerResult Reject(
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = false,
            Rejected = true,
            Messages = messages,
        };
    }

    /// <summary>
    /// Signals a successful step that hands off to a different processor.
    /// The framework will propagate <paramref name="targetProcessorName"/> to the
    /// HTTP response so the consumer can fetch authoritative state from the target.
    /// </summary>
    /// <param name="targetProcessorName">
    /// The service that takes the process over; another service's name. Handing off to this
    /// service itself is reported as a process violation.
    /// </param>
    /// <param name="messages">Business messages returned to the caller.</param>
    /// <exception cref="ArgumentException"><paramref name="targetProcessorName"/> is null, empty or whitespace.</exception>
    public static ProcessStepHandlerResult HandOff(
        string targetProcessorName,
        params ProcessMessage[] messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetProcessorName);

        return new()
        {
            Succeeded = true,
            TargetProcessorName = targetProcessorName,
            Messages = messages,
        };
    }
    /// <summary>
    /// Signals a successful step with a typed response and no specific next step required; the
    /// runtime continues with the steps the process rules make available. Chain
    /// <see cref="ProcessStepHandlerResult{TProcessStepResult}.Next{TNext}"/> or
    /// <see cref="ProcessStepHandlerResult{TProcessStepResult}.RequireInformation{TNext}"/> to require
    /// the next step.
    /// </summary>
    /// <typeparam name="TProcessStepResult">The typed response, inferred from <paramref name="response"/>.</typeparam>
    /// <param name="response">The typed response payload.</param>
    /// <param name="messages">Business messages returned to the caller.</param>
    /// <example>
    /// <code>
    /// return ProcessStepHandlerResult.Success(response);
    /// </code>
    /// </example>
    public static ProcessStepHandlerResult<TProcessStepResult> Success<TProcessStepResult>(
        TProcessStepResult response,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            Messages = messages,
            Response = response,
        };
    }

    /// <summary>
    /// Signals a successful step with a typed response that completes the whole process (see
    /// <see cref="Complete(ProcessMessage[])"/>).
    /// </summary>
    /// <typeparam name="TProcessStepResult">The typed response, inferred from <paramref name="response"/>.</typeparam>
    /// <param name="response">The typed response payload.</param>
    /// <param name="messages">Business messages returned to the caller.</param>
    public static ProcessStepHandlerResult<TProcessStepResult> Complete<TProcessStepResult>(
        TProcessStepResult response,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            CompletesProcess = true,
            Messages = messages,
            Response = response,
        };
    }

    /// <summary>
    /// Signals a business failure with a typed response: the request cannot proceed, and the process
    /// ends in <c>BusinessFailure</c>: no further step runs on it. For input the caller can correct, use
    /// <see cref="Reject"/>.
    /// </summary>
    /// <typeparam name="TProcessStepResult">The typed response, inferred from <paramref name="response"/>.</typeparam>
    /// <param name="response">The typed response payload.</param>
    /// <param name="messages">Business messages explaining the failure.</param>
    public static ProcessStepHandlerResult<TProcessStepResult> Failure<TProcessStepResult>(
        TProcessStepResult response,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = false,
            Messages = messages,
            Response = response,
        };
    }
}
