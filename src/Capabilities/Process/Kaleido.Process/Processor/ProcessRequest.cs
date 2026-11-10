namespace Kaleido.Processor;

/// <summary>
/// A request to <see cref="IProcessorRuntime.ExecuteAsync"/>: the process to continue (or none, to
/// start one) and the steps submitted for it.
/// </summary>
/// <remarks>
/// Use <see cref="ForStep{TStep}"/> to submit a single typed step.
/// </remarks>
internal sealed record ProcessRequest
{
    /// <summary>
    /// Uniquely identifies the durable process instance.
    /// Re-submissions for the same process use the same correlation id.
    /// Null creates a new process instance.
    /// </summary>
    public Guid? ProcessId
    {
        get;
        init;
    }

    /// <summary>
    /// Consumer supplied process request data.
    /// </summary>
    public required ProcessorRequest Processor
    {
        get;
        init;
    }

    /// <summary>
    /// Creates a <see cref="ProcessRequest"/> for a single step.
    /// The step name is the step type's name (<c>typeof(TStep).Name</c>), the same
    /// name the registry publishes.
    /// </summary>
    /// <typeparam name="TStep">The process step type.</typeparam>
    /// <param name="step">The step instance containing the input data.</param>
    /// <param name="processId">
    /// Optional process correlation identifier. Pass the same value on each submission
    /// to resume an existing process instance. Omit (or pass <see langword="null"/>) to start a new one.
    /// </param>
    public static ProcessRequest ForStep<TStep>(TStep step, Guid? processId = null)
        where TStep : class, IProcessStep
    {
        ArgumentNullException.ThrowIfNull(step);

        var name = typeof(TStep).Name;

        return new ProcessRequest
        {
            ProcessId = processId,
            Processor = new ProcessorRequest
            {
                Steps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    [name] = step,
                },
            },
        };
    }
}
