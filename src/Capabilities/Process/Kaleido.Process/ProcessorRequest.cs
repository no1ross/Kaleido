namespace Kaleido.Process;

/// <summary>
/// The typed steps submitted together to one processor, carried in
/// <see cref="ProcessRequest.Processor"/>. A processor may plan several steps in one call.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessorRequest
{
    /// <summary>
    /// Submitted steps keyed by their published names (case-insensitive). Local runtime calls use
    /// registered <see cref="IProcessStep"/> instances; outbound clients may use typed local mirror
    /// objects under the remote step's published name. Neither stores an unparsed HTTP body here.
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
