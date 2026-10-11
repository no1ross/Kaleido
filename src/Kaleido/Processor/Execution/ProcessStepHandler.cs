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
/// <remarks>
/// Created with <see cref="ProcessStepHandlerResult.Success{TProcessStepResult}(TProcessStepResult, ProcessMessage[])"/>
/// or <see cref="ProcessStepHandlerResult.Failure{TProcessStepResult}(TProcessStepResult, ProcessMessage[])"/>;
/// results without a response (<see cref="ProcessStepHandlerResult.Reject"/>,
/// <see cref="ProcessStepHandlerResult.HandOff"/>) convert to it implicitly.
/// </remarks>
/// <example>
/// <code>
/// return ProcessStepHandlerResult.Success(response).Next&lt;ShipOrder&gt;();
/// return ProcessStepHandlerResult.Reject(messages);
/// </code>
/// </example>
/// <typeparam name="TProcessStepResult">The typed response returned to the caller.</typeparam>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepHandlerResult<TProcessStepResult> : IProcessStepHandlerResult
{
    internal ProcessStepHandlerResult() { }

    /// <inheritdoc />
    public bool Succeeded { get; init; }

    /// <inheritdoc />
    public bool Rejected { get; init; }

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
    /// Requires <typeparamref name="TNext"/> to execute next after this successful step.
    /// </summary>
    /// <typeparam name="TNext">
    /// The step that must execute next. It must be a legal next step under the process rules;
    /// otherwise the runtime reports a process violation. An information step is required with
    /// <see cref="RequireInformation{TNext}"/>, so there are questions to answer.
    /// </typeparam>
    /// <returns>A copy of this result with the required next step.</returns>
    /// <exception cref="KaleidoFrameworkException">
    /// This result is not a success in the current processor (a failure, a rejection, or a hand-off).
    /// </exception>
    /// <example>
    /// <code>return ProcessStepHandlerResult.Success(response).Next&lt;ShipOrder&gt;();</code>
    /// </example>
    public ProcessStepHandlerResult<TProcessStepResult> Next<TNext>()
        where TNext : IProcessStep
    {
        EnsureCanRequireNextStep(typeof(TNext));

        return this with { RequiredStep = typeof(TNext) };
    }

    /// <summary>
    /// Requires the information step <typeparamref name="TNext"/> next, with the questions it needs
    /// answered. The process waits in <c>AwaitingInformation</c> until the answers are submitted as
    /// <typeparamref name="TNext"/>'s payload.
    /// </summary>
    /// <typeparam name="TNext">
    /// The information step that receives the answers. It must be a legal next step under the
    /// process rules; to loop, a <see cref="RepeatableAttribute">[Repeatable]</see> information
    /// step can require itself with the next request.
    /// </typeparam>
    /// <param name="request">The questions; the content is the implementer's, the shape is Kaleido's.</param>
    /// <returns>A copy of this result with the required information step and its request.</returns>
    /// <exception cref="KaleidoFrameworkException">
    /// This result is not a success in the current processor (a failure, a rejection, or a hand-off).
    /// </exception>
    /// <example>
    /// <code>return ProcessStepHandlerResult.Success(response).RequireInformation&lt;AnswerQuestions&gt;(request);</code>
    /// </example>
    public ProcessStepHandlerResult<TProcessStepResult> RequireInformation<TNext>(InformationRequest request)
        where TNext : IInformationStep
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCanRequireNextStep(typeof(TNext));

        return this with { RequiredStep = typeof(TNext), InformationRequest = request };
    }

    /// <summary>
    /// Uses a result without a response - <see cref="ProcessStepHandlerResult.Reject"/>,
    /// <see cref="ProcessStepHandlerResult.HandOff"/>, or an untyped success or failure - where a
    /// typed result is returned.
    /// </summary>
    /// <param name="result">The untyped result.</param>
    /// <exception cref="KaleidoFrameworkException">
    /// <paramref name="result"/> carries a response that is not a <typeparamref name="TProcessStepResult"/>.
    /// </exception>
    public static implicit operator ProcessStepHandlerResult<TProcessStepResult>(ProcessStepHandlerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new()
        {
            Succeeded = result.Succeeded,
            Rejected = result.Rejected,
            RequiredStep = result.RequiredStep,
            InformationRequest = result.InformationRequest,
            TargetProcessorName = result.TargetProcessorName,
            Messages = result.Messages,
            Response = result.Response switch
            {
                null => default,
                TProcessStepResult response => response,
                var other => throw new KaleidoFrameworkException(
                    ProcessorErrorCodes.InvalidRequiredStep,
                    $"A result with a '{other.GetType().Name}' response cannot be returned as ProcessStepHandlerResult<{typeof(TProcessStepResult).Name}>."),
            },
        };
    }

    private void EnsureCanRequireNextStep(Type next)
    {
        if (!Succeeded || Rejected || TargetProcessorName is not null)
        {
            throw new KaleidoFrameworkException(
                ProcessorErrorCodes.InvalidRequiredStep,
                $"Only a successful result in the current processor can require '{next.Name}' next.");
        }
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
    public bool Rejected { get; init; }

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
    /// Signals a business failure: the request cannot proceed, and the process ends in
    /// <c>BusinessFailure</c>. For input the caller can correct, use <see cref="Reject"/>.
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
    public static ProcessStepHandlerResult HandOff(
        string targetProcessorName,
        params ProcessMessage[] messages)
    {
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
    /// <code>return ProcessStepHandlerResult.Success(response);</code>
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
    /// Signals a business failure with a typed response: the request cannot proceed, and the process
    /// ends in <c>BusinessFailure</c>. For input the caller can correct, use <see cref="Reject"/>.
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
