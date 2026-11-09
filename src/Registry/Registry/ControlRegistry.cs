using Kaleido.Platform.Registry;
using Kaleido.Server.Authorization;

namespace Kaleido.Registry;

// The control registry's writes (#553): an instance registers, heartbeats and deregisters. Each is checked
// against the services the caller may register - its app roles naming them, and the optional registrants
// map - unless the host's AuthorizationMode is None (the endpoints check RegistrantPolicy first).
// A heartbeat and a deregistration are one conditional write each: the store checks the instance's
// service against what the caller may register in the same statement.
internal interface IControlRegistry
{
    Task<RegistryAnswer> RegisterAsync(RegistryInstanceRegistration registration, RegistryCaller registrant, CancellationToken cancellationToken);

    Task<RegistryAnswer> HeartbeatAsync(Guid instanceId, RegistryHeartbeat heartbeat, RegistryCaller registrant, CancellationToken cancellationToken);

    Task<RegistryAnswer> DeregisterAsync(Guid instanceId, RegistryCaller registrant, CancellationToken cancellationToken);
}

internal sealed class ControlRegistry(
    IKaleidoRegistryStore store,
    KaleidoRegistryOptions options,
    IRegistryRegistrants registrants,
    IKaleidoAuthorizer authorizer,
    TimeProvider time)
    : IControlRegistry
{
    public async Task<RegistryAnswer> RegisterAsync(RegistryInstanceRegistration registration, RegistryCaller registrant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (Invalid(registration) is { } problem)
        {
            return RegistryAnswer.Failed(RegistryAnswerKind.Invalid, RegistryErrorCodes.InvalidRegistration, problem);
        }

        if (authorizer.IsEnforced && !registrants.MayRegister(registrant, registration.ServiceName))
        {
            return NotAllowed();
        }

        var now = time.GetUtcNow();

        // One record per service version and revision: the capabilities are stamped with the version and
        // stored once; every instance of that build adds only its own row.
        var transports = registration.Transports.Count == 0
            ? [new RegistryTransportRoute { Transport = "http", GatewayRoute = $"/{registration.ServiceName}/{registration.ServiceVersion}" }]
            : registration.Transports.Select(x => x with { Transport = x.Transport.ToLowerInvariant() }).ToArray();
        var capabilities = new KaleidoRegistry
        {
            Processes = [.. registration.Processes.Select(x => x with { ServiceVersion = registration.ServiceVersion })],
            Queryables = [.. registration.Queryables.Select(x => x with { ServiceVersion = registration.ServiceVersion })],
            Routes = [new RegistryServiceRoute { ServiceName = registration.ServiceName, ServiceVersion = registration.ServiceVersion, Transports = transports }],
        }.ToEntryJson();
        var revision = capabilities.ToRevision();

        await store.AddRegistrationAsync(
            new RegistryRegistrationRecord { ServiceName = registration.ServiceName, ServiceVersion = registration.ServiceVersion, Revision = revision, RegisteredUtc = now },
            capabilities,
            cancellationToken);
        await store.SaveInstanceAsync(
            new RegistryInstanceRecord { InstanceId = registration.InstanceId, ServiceName = registration.ServiceName, ServiceVersion = registration.ServiceVersion, Revision = revision, RegisteredUtc = now, LastHeartbeatUtc = now },
            cancellationToken);

        // The first version registered becomes the service's current one; a later one never takes over here.
        await store.GetOrSetCurrentVersionAsync(registration.ServiceName, registration.ServiceVersion, cancellationToken);

        return RegistryAnswer.Leased(Lease(registration.InstanceId, now));
    }

    public async Task<RegistryAnswer> HeartbeatAsync(Guid instanceId, RegistryHeartbeat heartbeat, RegistryCaller registrant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        var now = time.GetUtcNow();
        var change = await store.RenewInstanceAsync(instanceId, now, heartbeat.Healthy, AllowedServices(registrant), cancellationToken);

        return change == RegistryInstanceChange.Done ? RegistryAnswer.Leased(Lease(instanceId, now)) : Refused(change, instanceId);
    }

    public async Task<RegistryAnswer> DeregisterAsync(Guid instanceId, RegistryCaller registrant, CancellationToken cancellationToken)
    {
        var change = await store.RemoveInstanceAsync(instanceId, AllowedServices(registrant), cancellationToken);

        return change == RegistryInstanceChange.Done ? RegistryAnswer.Removed : Refused(change, instanceId);
    }

    // Null (any service) when authorization is not enforced (development only).
    private IReadOnlyCollection<string>? AllowedServices(RegistryCaller registrant) =>
        authorizer.IsEnforced ? registrants.ServicesOf(registrant) : null;

    private RegistryLease Lease(Guid instanceId, DateTimeOffset now) =>
        new() { InstanceId = instanceId, HeartbeatInterval = options.HeartbeatInterval, LeaseUntilUtc = now + options.Lease };

    private static string? Invalid(RegistryInstanceRegistration registration) =>
        !KaleidoServiceOptions.IsValidServiceName(registration.ServiceName)
            ? $"'{registration.ServiceName}' is not a service name."
            : !KaleidoServiceOptions.IsValidServiceVersion(registration.ServiceVersion)
                ? "Every registration names its service version: 1 to 64 ASCII letters, digits, '.', '-' or '_', starting with a letter or digit."
                : registration.InstanceId == Guid.Empty
                    ? "The instance id is empty."
                    : registration.Processes.Select(x => x.ServiceName).Concat(registration.Queryables.Select(x => x.ServiceName))
                        .Any(x => !string.Equals(x, registration.ServiceName, StringComparison.OrdinalIgnoreCase))
                        ? $"A registration of '{registration.ServiceName}' carries capabilities of another service."
                        : InvalidTransports(registration) ?? InvalidPublishedUrls(registration);

    private static string? InvalidTransports(RegistryInstanceRegistration registration)
    {
        if (registration.Transports is null)
        {
            return "A registration's transports must be a collection.";
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var transport in registration.Transports)
        {
            if (transport is null || string.IsNullOrWhiteSpace(transport.Transport) || !seen.Add(transport.Transport)
                || transport.Transport.ToLowerInvariant() is not ("http" or "grpc"))
            {
                return "Each transport must be 'http' or 'grpc', and each may be declared only once.";
            }

            if (!ValidGatewayRoute(transport.GatewayRoute, registration.ServiceName, registration.ServiceVersion,
                    transport.Transport.Equals("http", StringComparison.OrdinalIgnoreCase)))
            {
                return $"The {transport.Transport} gateway route must be a versioned path for '{registration.ServiceName}/{registration.ServiceVersion}', without an origin, traversal, query or fragment.";
            }
        }

        return null;
    }

    private static string? InvalidPublishedUrls(RegistryInstanceRegistration registration)
    {
        var processUrls = registration.Processes.SelectMany(p =>
            new string?[] { p.ExecuteUrl, p.ProcessStateUrlTemplate }
                .Concat(p.InitialSteps.Select(x => x.ExecuteUrl))
                .Concat((p.Steps ?? []).SelectMany(s => new string?[] { s.ExecuteUrl }
                    .Concat(s.Dependencies.Select(x => x.ExecuteUrl))
                    .Concat(s.AvailableAfter.Select(x => x.ExecuteUrl))
                    .Concat(s.AvailableUntil.Select(x => x.ExecuteUrl)))));
        var queryUrls = registration.Queryables.SelectMany(q =>
            new string?[] { q.QueryUrl }.Concat(q.Views.Select(x => x.QueryUrl)));
        return processUrls.Concat(queryUrls).Any(url => !string.IsNullOrEmpty(url) && !ValidPublishedUrl(url, registration.ServiceName, registration.ServiceVersion))
            ? $"A capability URL must be a safe relative path beneath /{registration.ServiceName}/{registration.ServiceVersion}/."
            : null;
    }

    private static bool ValidPublishedUrl(string url, string service, string version) =>
        url.StartsWith($"/{service}/{version}/", StringComparison.Ordinal)
        && url.IndexOfAny(['%', '\\', '?', '#']) < 0
        && url.Split('/').Skip(1).All(segment => segment.Length > 0 && segment is not ("." or ".."));

    private static bool ValidGatewayRoute(string? route, string service, string version, bool isHttp)
    {
        if (route is null || route.Length > 512)
        {
            return false;
        }

        var segments = route.Split('/');
        return segments.Length >= 3 && segments[0].Length == 0
            && segments.Skip(1).All(x => x.Length > 0 && x is not ("." or "..") && x.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            && string.Equals(segments[^2], service, StringComparison.Ordinal)
            && string.Equals(segments[^1], version, StringComparison.Ordinal)
            && (!isHttp || segments.Length == 3);
    }

    private static RegistryAnswer Refused(RegistryInstanceChange change, Guid instanceId) =>
        change == RegistryInstanceChange.NotAllowed
            ? NotAllowed()
            : RegistryAnswer.Failed(RegistryAnswerKind.Unknown, RegistryErrorCodes.InstanceUnknown, $"Instance {instanceId} is not registered; register it again.");

    private static RegistryAnswer NotAllowed() =>
        RegistryAnswer.Failed(RegistryAnswerKind.NotAllowed, RegistryErrorCodes.RegistrantNotAllowed, "The caller may not register this service: it has no app role naming it (Kaleido:Registry:RegistrantRolePrefix) and the Kaleido:Registry:Registrants map does not list it.");
}
