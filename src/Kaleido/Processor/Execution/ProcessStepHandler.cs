namespace Kaleido.Processor.Execution;

/// <summary>
/// Executes a process step and returns a typed response alongside the outcome.
/// </summary>
/// <typeparam name="TProcessStep">The step type this handler executes.</typeparam>
/// <typeparam name="TProcessStepResult">The typed response returned to the caller.</typeparam>
/// <remarks>
/// Exactly one handler must exist per <see cref="IProcessStep"/> type; startup fails
/// when a step has no handler or more than one. Handlers run in their own DI scope
/// with the request's correlation context.
/// </remarks>
public interface IProcessStepHandler<in TProcessStep, TProcessStepResult>
    where TProcessStep : IProcessStep
{
    /// <summary>
    /// Executes <paramref name="processStep"/>.
    /// </summary>
    /// <param name="processStep">The validated step input.</param>
    /// <param name="context">The executing process and step context.</param>
    /// <param name="cancellationToken">Cancels the execution.</param>
    /// <returns>
    /// The step outcome, built with <see cref="ProcessStepHandlerResult{TProcessStepResult}"/>
    /// factory methods.
    /// </returns>
    Task<ProcessStepHandlerResult<TProcessStepResult>> ExecuteAsync(
        TProcessStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Executes a process step that returns no typed response.
/// </summary>
/// <typeparam name="TProcessStep">The step type this handler executes.</typeparam>
/// <remarks>
/// Exactly one handler must exist per <see cref="IProcessStep"/> type; startup fails
/// when a step has no handler or more than one. Handlers run in their own DI scope
/// with the request's correlation context.
/// </remarks>
public interface IProcessStepHandler<in TProcessStep>
    where TProcessStep : IProcessStep
{
    /// <summary>
    /// Executes <paramref name="processStep"/>.
    /// </summary>
    /// <param name="processStep">The validated step input.</param>
    /// <param name="context">The executing process and step context.</param>
    /// <param name="cancellationToken">Cancels the execution.</param>
    /// <returns>
    /// The step outcome, built with <see cref="ProcessStepHandlerResult"/> factory methods.
    /// </returns>
    Task<ProcessStepHandlerResult> ExecuteAsync(
        TProcessStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome a step handler returns to the runtime.
/// </summary>
/// <remarks>
/// Create instances through the factory methods on
/// <see cref="ProcessStepHandlerResult"/> and
/// <see cref="ProcessStepHandlerResult{TProcessStepResult}"/>. The next step is
/// always identified by type; its public name is resolved by the runtime only when
/// state, events, and responses are written.
/// </remarks>
public interface IProcessStepHandlerResult
{
    /// <summary>
    /// <see langword="true"/> when the step succeeded; <see langword="false"/> for a
    /// business failure.
    /// </summary>
    bool Succeeded { get; }

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
    /// The step's response payload, if any.
    /// </summary>
    object? Response { get; }

    /// <summary>
    /// Business messages returned to the caller.
    /// </summary>
    IReadOnlyCollection<ProcessMessage> Messages { get; }
}

/// <summary>
/// The outcome of a step handler that returns a typed response.
/// </summary>
/// <typeparam name="TProcessStepResult">The typed response returned to the caller.</typeparam>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepHandlerResult<TProcessStepResult> : IProcessStepHandlerResult
{
    internal ProcessStepHandlerResult() { }

    /// <inheritdoc />
    public bool Succeeded { get; init; }

    /// <inheritdoc />
    public Type? RequiredStep { get; init; }

    /// <inheritdoc />
    public InformationRequest? InformationRequest { get; init; }

    /// <inheritdoc />
    public string? TargetProcessorName { get; init; }

    /// <summary>
    /// The typed response payload.
    /// </summary>
    public TProcessStepResult? Response { get; init; }

    object? IProcessStepHandlerResult.Response => Response;

    /// <inheritdoc />
    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];

    /// <summary>
    /// Signals a successful step with no specific next step required; the runtime
    /// continues with the steps the process rules make available.
    /// </summary>
    /// <param name="response">The typed response payload.</param>
    /// <param name="messages">Business messages returned to the caller.</param>
    public static ProcessStepHandlerResult<TProcessStepResult> Success(
        TProcessStepResult response,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            Messages = messages,
            Response = response
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
    /// <param name="response">The typed response payload.</param>
    /// <param name="messages">Business messages returned to the caller.</param>
    public static ProcessStepHandlerResult<TProcessStepResult> Success<TNext>(
        TProcessStepResult response,
        params ProcessMessage[] messages)
        where TNext : IProcessStep
    {
        return new()
        {
            Succeeded = true,
            RequiredStep = typeof(TNext),
            Messages = messages,
            Response = response
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
    /// <param name="response">The typed response payload.</param>
    /// <param name="request">The questions; the content is the implementer's, the shape is Kaleido's.</param>
    /// <param name="messages">Business messages returned to the caller.</param>
    public static ProcessStepHandlerResult<TProcessStepResult> RequireInformation<TNext>(
        TProcessStepResult response,
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
            Response = response
        };
    }

    /// <summary>
    /// Signals a business failure.
    /// </summary>
    /// <param name="response">The typed response payload.</param>
    /// <param name="messages">Business messages explaining the failure.</param>
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
    public static ProcessStepHandlerResult<TProcessStepResult> HandOff(
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

/// <summary>
/// The outcome of a step handler that returns no typed response.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepHandlerResult : IProcessStepHandlerResult
{
    internal ProcessStepHandlerResult() { }

    /// <inheritdoc />
    public bool Succeeded { get; init; }

    /// <inheritdoc />
    public Type? RequiredStep { get; init; }

    /// <inheritdoc />
    public InformationRequest? InformationRequest { get; init; }

    /// <inheritdoc />
    public string? TargetProcessorName { get; init; }

    /// <inheritdoc />
    public object? Response { get; init; }

    /// <inheritdoc />
    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
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
            Messages = messages
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
            Messages = messages
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
            Messages = messages
        };
    }

    /// <summary>
    /// Signals a business failure.
    /// </summary>
    /// <param name="messages">Business messages explaining the failure.</param>
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
