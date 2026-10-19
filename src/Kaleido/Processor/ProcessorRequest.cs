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
    /// A value may be an instance of the step type, used as is, or any value that deserializes to
    /// it (such as a <c>JsonElement</c> from an HTTP request). Each entry becomes one planned
    /// candidate; a name that is not a registered step, or a value that cannot be read as the step,
    /// makes that candidate invalid. The default dictionary compares names case-insensitively.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Steps { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
