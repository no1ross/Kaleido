namespace Kaleido.Client.Queryable;

/// <summary>Creates Queryable clients for services published by the central registry.</summary>
public interface IKaleidoQueryableClientFactory
{
    /// <summary>Queries the current version of a service, as the central registry names it.</summary>
    /// <param name="serviceName">The published service name, compared without case.</param>
    /// <returns>A client that resolves each query from the central registry's hydrated copy.</returns>
    IKaleidoQueryableClient GetClient(string serviceName);

    /// <summary>Queries the specified version of a service, including one used by a running process.</summary>
    /// <param name="serviceName">The published service name, compared without case.</param>
    /// <param name="serviceVersion">The version whose published query routes the calls must use.</param>
    /// <returns>A client pinned to that service version.</returns>
    IKaleidoQueryableClient GetClient(string serviceName, string serviceVersion);
}
