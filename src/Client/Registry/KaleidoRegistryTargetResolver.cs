namespace Kaleido.Client.Registry;

internal interface IKaleidoRegistryTargetResolver
{
    Task<KaleidoRegistryTarget> ResolveAsync(string serviceName, string? serviceVersion, IReadOnlyCollection<string> supportedTransports, CancellationToken cancellationToken);
}

internal sealed record KaleidoRegistryTarget(KaleidoRegistry Registry, RegistryServiceAvailability Service, RegistryTransportRoute Transport);

internal sealed class KaleidoRegistryTargetResolver(IKaleidoRegistrySource source, IKaleidoTransportSelector selector) : IKaleidoRegistryTargetResolver
{
    public async Task<KaleidoRegistryTarget> ResolveAsync(string serviceName, string? serviceVersion, IReadOnlyCollection<string> supportedTransports, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(supportedTransports);

        var availability = await source.GetAvailabilityAsync(cancellationToken);
        var service = availability.Services.FirstOrDefault(x =>
            string.Equals(x.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)
            && (serviceVersion is null ? x.IsCurrent : string.Equals(x.ServiceVersion, serviceVersion, StringComparison.Ordinal)));
        if (service is null)
        {
            throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The central registry has no route for service '{serviceName}' version '{serviceVersion ?? "current"}'.");
        }

        var transport = selector.Select(service.Transports, supportedTransports)
            ?? throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The central registry has no supported transport for service '{serviceName}' version '{service.ServiceVersion}'.");
        return new KaleidoRegistryTarget(await source.GetAsync(cancellationToken), service, transport);
    }
}
