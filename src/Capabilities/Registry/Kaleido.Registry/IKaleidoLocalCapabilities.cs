namespace Kaleido.Registry;

/// <summary>
/// This host's own capabilities, in the shape the registry publishes them (#553): what it registers with
/// the control registry. Process and Queryable contribute their own models; transports add operation bindings.
/// </summary>
internal interface IKaleidoLocalCapabilities
{
    /// <summary>This host's processors and query sources, no one else's.</summary>
    KaleidoRegistry Capabilities();
}

internal interface IProcessRegistryBindingContributor
{
    ProcessRegistryItem Bind(ProcessRegistryItem item, KaleidoServiceOptions service);
}

internal interface IQueryableRegistryBindingContributor
{
    QueryableRegistryItem Bind(QueryableRegistryItem item, KaleidoServiceOptions service);
}
