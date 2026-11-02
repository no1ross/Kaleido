namespace Kaleido.Http.Registry;

// Who may register what (Kaleido:Registry:Registrants), turned around once at startup: the services
// each registrant (a client id) may register, renew and remove.
internal interface IRegistryRegistrants
{
    IReadOnlyCollection<string> ServicesOf(string? registrant);

    bool MayRegister(string? registrant, string serviceName);
}

internal sealed class RegistryRegistrants(KaleidoRegistryOptions options) : IRegistryRegistrants
{
    private readonly Dictionary<string, string[]> _servicesByClient = options.Registrants
        .SelectMany(x => x.Value.Select(client => (Client: client, Service: x.Key)))
        .GroupBy(x => x.Client, StringComparer.Ordinal)
        .ToDictionary(x => x.Key, x => x.Select(y => y.Service).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.Ordinal);

    public IReadOnlyCollection<string> ServicesOf(string? registrant) =>
        registrant is not null && _servicesByClient.TryGetValue(registrant, out var services) ? services : [];

    public bool MayRegister(string? registrant, string serviceName) =>
        ServicesOf(registrant).Contains(serviceName, StringComparer.OrdinalIgnoreCase);
}
