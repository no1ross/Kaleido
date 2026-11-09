namespace Kaleido.Client.Registry;

internal interface IKaleidoTransportSelector
{
    RegistryTransportRoute? Select(IReadOnlyCollection<RegistryTransportRoute> routes, IReadOnlyCollection<string> available);

    bool CanFallback(bool wasSent, bool isRead, bool hasIdempotencyKey);

    RegistryTransportRoute? Fallback(IReadOnlyCollection<RegistryTransportRoute> routes, IReadOnlyCollection<string> available,
        string attemptedTransport, bool wasSent, bool isRead, bool hasIdempotencyKey);
}

internal sealed class KaleidoTransportSelector : IKaleidoTransportSelector
{
    public RegistryTransportRoute? Select(
        IReadOnlyCollection<RegistryTransportRoute> routes,
        IReadOnlyCollection<string> available)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(available);

        foreach (var preferred in new[] { "grpc", "http" })
        {
            if (available.Any(x => string.Equals(x, preferred, StringComparison.OrdinalIgnoreCase))
                && routes.FirstOrDefault(x => string.Equals(x.Transport, preferred, StringComparison.OrdinalIgnoreCase)) is { } route)
            {
                return route;
            }
        }

        return null;
    }

    public bool CanFallback(bool wasSent, bool isRead, bool hasIdempotencyKey) =>
        !wasSent || isRead || hasIdempotencyKey;

    public RegistryTransportRoute? Fallback(IReadOnlyCollection<RegistryTransportRoute> routes, IReadOnlyCollection<string> available,
        string attemptedTransport, bool wasSent, bool isRead, bool hasIdempotencyKey) =>
        CanFallback(wasSent, isRead, hasIdempotencyKey)
            ? Select([.. routes.Where(x => !string.Equals(x.Transport, attemptedTransport, StringComparison.OrdinalIgnoreCase))], available)
            : null;
}
