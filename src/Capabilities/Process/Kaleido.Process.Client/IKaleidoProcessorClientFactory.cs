namespace Kaleido.Process.Client;

/// <summary>Creates transport-neutral Process clients for a named remote service and version.</summary>
public interface IKaleidoProcessorClientFactory
{
    /// <summary>Uses the currently published version when starting a new process.</summary>
    /// <param name="serviceName">The remote service's published name.</param>
    /// <returns>A client that resolves the current version through its transport adapter.</returns>
    IKaleidoProcessorClient GetClient(string serviceName);

    /// <summary>Pins all calls to one version, especially when continuing a handed-off process.</summary>
    /// <param name="serviceName">The remote service's published name.</param>
    /// <param name="serviceVersion">The target's actual selected version, such as <see cref="ProcessResult.TargetProcessorVersion"/>.</param>
    /// <returns>A client whose calls stay on this version even if another becomes current.</returns>
    IKaleidoProcessorClient GetClient(string serviceName, string serviceVersion);
}
