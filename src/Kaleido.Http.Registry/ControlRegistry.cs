using Kaleido.Http.Authorization;

namespace Kaleido.Http.Registry;

// The control registry's writes (#553): an instance registers, heartbeats and deregisters. Each is checked
// against who may register the service (the registrants map), unless the host's AuthorizationMode is None.
// A heartbeat and a deregistration are one conditional write each: the store checks the instance's
// service against what the caller may register in the same statement.
internal interface IControlRegistry
{
    Task<RegistryAnswer> RegisterAsync(RegistryInstanceRegistration registration, string? registrant, CancellationToken cancellationToken);

    Task<RegistryAnswer> HeartbeatAsync(Guid instanceId, RegistryHeartbeat heartbeat, string? registrant, CancellationToken cancellationToken);

    Task<RegistryAnswer> DeregisterAsync(Guid instanceId, string? registrant, CancellationToken cancellationToken);
}

internal sealed class ControlRegistry(
    IKaleidoRegistryStore store,
    KaleidoRegistryOptions options,
    IRegistryRegistrants registrants,
    IKaleidoAuthorizer authorizer,
    TimeProvider time)
    : IControlRegistry
{
    public async Task<RegistryAnswer> RegisterAsync(RegistryInstanceRegistration registration, string? registrant, CancellationToken cancellationToken)
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
        var capabilities = new RegistryCapabilities(
            [.. registration.Processes.Select(x => x with { ServiceVersion = registration.ServiceVersion })],
            [.. registration.Queryables.Select(x => x with { ServiceVersion = registration.ServiceVersion })]).ToJson();
        var revision = RegistryCapabilities.RevisionOf(capabilities);

        await store.AddRegistrationAsync(
            new RegistryRegistrationRecord { ServiceName = registration.ServiceName, ServiceVersion = registration.ServiceVersion, Revision = revision, RegisteredUtc = now },
            capabilities,
            cancellationToken);
        await store.SaveInstanceAsync(
            new RegistryInstanceRecord { InstanceId = registration.InstanceId, ServiceName = registration.ServiceName, ServiceVersion = registration.ServiceVersion, Revision = revision, Address = registration.Address, RegisteredUtc = now, LastHeartbeatUtc = now },
            cancellationToken);

        // The first version registered becomes the service's current one; a later one never takes over here.
        await store.GetOrSetCurrentVersionAsync(registration.ServiceName, registration.ServiceVersion, cancellationToken);

        return RegistryAnswer.Leased(Lease(registration.InstanceId, now));
    }

    public async Task<RegistryAnswer> HeartbeatAsync(Guid instanceId, RegistryHeartbeat heartbeat, string? registrant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        var now = time.GetUtcNow();
        var change = await store.RenewInstanceAsync(instanceId, now, heartbeat.Healthy, AllowedServices(registrant), cancellationToken);

        return change == RegistryInstanceChange.Done ? RegistryAnswer.Leased(Lease(instanceId, now)) : Refused(change, instanceId);
    }

    public async Task<RegistryAnswer> DeregisterAsync(Guid instanceId, string? registrant, CancellationToken cancellationToken)
    {
        var change = await store.RemoveInstanceAsync(instanceId, AllowedServices(registrant), cancellationToken);

        return change == RegistryInstanceChange.Done ? RegistryAnswer.Removed : Refused(change, instanceId);
    }

    // Null (any service) when authorization is not enforced (development only).
    private IReadOnlyCollection<string>? AllowedServices(string? registrant) =>
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
                        : null;

    private static RegistryAnswer Refused(RegistryInstanceChange change, Guid instanceId) =>
        change == RegistryInstanceChange.NotAllowed
            ? NotAllowed()
            : RegistryAnswer.Failed(RegistryAnswerKind.Unknown, RegistryErrorCodes.InstanceUnknown, $"Instance {instanceId} is not registered; register it again.");

    private static RegistryAnswer NotAllowed() =>
        RegistryAnswer.Failed(RegistryAnswerKind.NotAllowed, RegistryErrorCodes.RegistrantNotAllowed, "The caller may not register this service (Kaleido:Registry:Registrants).");
}
