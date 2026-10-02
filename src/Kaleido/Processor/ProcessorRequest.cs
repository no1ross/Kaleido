namespace Kaleido.Processor;

[ExcludeFromCodeCoverage]
public sealed record ProcessorRequest
{
    public IReadOnlyDictionary<string, object?> Steps { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
