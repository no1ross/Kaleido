namespace Kaleido.Registry;

/// <summary>Call bindings contributed by the transports a service actually publishes.</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryTransportBindings
{
    /// <summary>HTTP operations, empty when this capability has no HTTP endpoint.</summary>
    public IReadOnlyCollection<RegistryHttpOperation> Http { get; init; } = [];
}

/// <summary>One named HTTP operation exposed through a versioned gateway path.</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryHttpOperation
{
    /// <summary>The capability operation, such as query, execute or state.</summary>
    public required string Operation { get; init; }

    /// <summary>The HTTP method used for this operation.</summary>
    public required string Method { get; init; }

    /// <summary>The published relative gateway path or path template.</summary>
    public required string Path { get; init; }
}
