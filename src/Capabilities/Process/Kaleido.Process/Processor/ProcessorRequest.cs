namespace Kaleido.Processor;

/// <summary>
/// The steps submitted to a processor in one request, the <see cref="ProcessRequest.Processor"/>
/// part of a <see cref="ProcessRequest"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ProcessorRequest
{
    /// <summary>
    /// The submitted steps, keyed by step name (the step type's name, as the registry publishes it)
    /// with the step's input as the value.
    /// </summary>
    /// <remarks>
    /// Each value is an instance of the step type: the transport reads its wire format into the step
    /// type before the runtime sees it (core never parses input). Each entry becomes one planned
    /// candidate; a name that is not a registered step, or a value of another type, makes that
    /// candidate invalid. The default dictionary compares names case-insensitively.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Steps { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
