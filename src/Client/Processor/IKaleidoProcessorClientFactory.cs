namespace Kaleido.Client.Processor;

/// <summary>Creates Process clients for services published by the central registry.</summary>
public interface IKaleidoProcessorClientFactory
{
    /// <summary>Calls the current version of a service, as the central registry names it.</summary>
    /// <param name="serviceName">The published service name, compared without case.</param>
    /// <returns>A client that resolves each call from the central registry's hydrated copy.</returns>
    IKaleidoProcessorClient GetClient(string serviceName);

    /// <summary>Calls the specified version of a service, preserving the version of a running process.</summary>
    /// <param name="serviceName">The published service name, compared without case.</param>
    /// <param name="serviceVersion">The version whose published routes the calls must use.</param>
    /// <returns>A client pinned to that service version.</returns>
    IKaleidoProcessorClient GetClient(string serviceName, string serviceVersion);
}
