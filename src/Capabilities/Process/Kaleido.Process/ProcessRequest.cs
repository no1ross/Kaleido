namespace Kaleido.Process;

/// <summary>
/// A transport-neutral request containing typed Process steps for a new or existing process.
/// It is shared by local <see cref="IProcessorRuntime.ExecuteAsync"/> and outbound Process clients;
/// it is not a JSON request body.
/// </summary>
/// <remarks>
/// Use <see cref="ForStep{TStep}"/> for one declared step. To submit several steps in one
/// request, provide a <see cref="ProcessorRequest"/> whose <see cref="ProcessorRequest.Steps"/>
/// map contains their registered type names and typed instances. A remote caller may put a local
/// mirror object under the target's published step name instead of implementing <see cref="IProcessStep"/>
/// in the calling service. The runtime plans them under the process rules and reports each
/// submitted step's outcome in <see cref="ProcessResult.Steps"/>.
/// The host authenticates and authorizes those steps before calling the runtime.
/// </remarks>
public sealed record ProcessRequest
{
    /// <summary>
    /// Identifies the durable process instance to continue. Use the id returned in
    /// <see cref="ProcessResult.ProcessId"/> for subsequent requests; <see langword="null"/>
    /// starts a new instance. This is not the ambient request-correlation id.
    /// </summary>
    public Guid? ProcessId
    {
        get;
        init;
    }

    /// <summary>
    /// The submitted steps as typed instances, keyed by their registered type names. For one
    /// step, use <see cref="ForStep{TStep}"/> instead of constructing this property by hand.
    /// </summary>
    public required ProcessorRequest Processor
    {
        get;
        init;
    }

    /// <summary>
    /// Creates a local <see cref="ProcessRequest"/> for one typed step. Its key is
    /// <c>typeof(TStep).Name</c>, the name that Process registered for this step type.
    /// </summary>
    /// <typeparam name="TStep">The process step type.</typeparam>
    /// <param name="step">The step instance containing the input data.</param>
    /// <param name="processId">
    /// The existing process id to continue; omit or pass <see langword="null"/> to start a new
    /// instance. Use the previous result's <see cref="ProcessResult.ProcessId"/> to resume it.
    /// </param>
    /// <returns>A request whose submitted steps contain this typed step under its registered name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="step"/> is null.</exception>
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
