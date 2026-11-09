namespace Kaleido.Registry;

// The caller of a registration, heartbeat or deregistration, as its validated token describes it: its
// identity (the actor - act, azp or appid - else its subject) and its roles.
internal sealed record RegistryCaller(string? Id, IReadOnlyCollection<string> Roles);

// Which services a caller may register, renew and remove (#553), from what the IdP assigned it:
// - each app role {RegistrantRolePrefix}{service} (Kaleido.Registrant.code-sets), assigned to the service's
//   app registration - no per-service configuration in the registry;
// - and, as a fallback, Kaleido:Registry:Registrants (service -> client ids), turned around once at startup.
// A caller may register the union of both. Service names compare without case.
internal interface IRegistryRegistrants
{
    IReadOnlyCollection<string> ServicesOf(RegistryCaller caller);

    bool MayRegister(RegistryCaller caller, string serviceName);
}

internal sealed class RegistryRegistrants(KaleidoRegistryOptions options) : IRegistryRegistrants
{
    private readonly Dictionary<string, string[]> _servicesByClient = options.Registrants
        .SelectMany(x => x.Value.Select(client => (Client: client, Service: x.Key)))
        .GroupBy(x => x.Client, StringComparer.Ordinal)
        .ToDictionary(x => x.Key, x => x.Select(y => y.Service).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.Ordinal);

    public IReadOnlyCollection<string> ServicesOf(RegistryCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var mapped = caller.Id is not null && _servicesByClient.TryGetValue(caller.Id, out var services) ? services : [];
        var assigned = caller.Roles
            .Where(x => x.Length > options.RegistrantRolePrefix.Length && x.StartsWith(options.RegistrantRolePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(x => x[options.RegistrantRolePrefix.Length..].ToLowerInvariant());

        return [.. mapped.Concat(assigned).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public bool MayRegister(RegistryCaller caller, string serviceName) =>
        ServicesOf(caller).Contains(serviceName, StringComparer.OrdinalIgnoreCase);
}
