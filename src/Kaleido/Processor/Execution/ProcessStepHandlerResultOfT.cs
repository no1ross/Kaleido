namespace Kaleido.Processor.Execution;

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
    /// <code>
    /// return ProcessStepHandlerResult.Success(response).Next&lt;ShipOrder&gt;();
    /// </code>
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
    /// <code>
    /// return ProcessStepHandlerResult.Success(response).RequireInformation&lt;AnswerQuestions&gt;(request);
    /// </code>
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
                _ => throw new KaleidoFrameworkException(
                    ProcessorErrorCodes.InvalidRequiredStep,
                    $"A result with a '{result.Response.GetType().Name}' response cannot be returned as ProcessStepHandlerResult<{typeof(TProcessStepResult).Name}>."),
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
