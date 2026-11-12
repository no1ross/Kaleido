namespace Kaleido.Process;

/// <summary>
/// The typed steps submitted together to one processor, carried in
/// <see cref="ProcessRequest.Processor"/>. A processor may plan several steps in one call.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessorRequest
{
    /// <summary>
    /// The submitted steps keyed by their declared CLR type names (case-insensitive). Each value
    /// is an instance of that registered <see cref="IProcessStep"/> type, never an unparsed body.
    /// </summary>
    /// <remarks>
    /// The runtime plans each entry as a candidate. Unknown names, mismatched value types and
    /// invalid step inputs are reported in the corresponding <see cref="ProcessStepResult"/>,
    /// rather than silently converted. The default empty dictionary uses case-insensitive names.
    /// A host adapter converts any wire input to typed steps before constructing this request.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Steps { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
