namespace Kaleido.Registry.Server.UnitTests;

// Registrations, instances and capabilities for the registry's tests.
internal static class RegistryTestData
{
    public static readonly DateTimeOffset Start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    public static ProcessRegistryItem ProcessorWith(string service, params string[] steps) =>
        new()
        {
            Name = service,
            ServiceName = service,
            InitialSteps = [.. steps.Select(x => new ProcessStepSummary { Name = x })],
            Steps = [.. steps.Select(x => new ProcessStepResponse { Name = x })],
        };

    public static QueryableRegistryItem SourceWith(string service, string name, params string[] views) =>
        new()
        {
            ServiceName = service,
            Name = name,
            Views = [.. views.Select(x => new QueryableRegistryViewItem { Name = x })],
        };

    // One build's registry entry: its processes and query sources.
    public static KaleidoRegistry Entry(IReadOnlyCollection<ProcessRegistryItem> processes, IReadOnlyCollection<QueryableRegistryItem> queryables) =>
        new() { Processes = processes, Queryables = queryables };

    // A registration and its model, as the store keeps them.
    public static (RegistryRegistrationRecord Record, KaleidoRegistry Capabilities) Registration(string service, string version, KaleidoRegistry entry, DateTimeOffset? registered = null, string buildVersion = "1.0.0-preview.1") =>
        (new RegistryRegistrationRecord
        {
            ServiceName = service,
            ServiceVersion = version,
            BuildVersion = buildVersion,
            RegisteredUtc = registered ?? Start,
        }, entry);

    public static RegistryInstanceRecord Instance(RegistryRegistrationRecord registration, DateTimeOffset lastHeartbeat, bool healthy = true) =>
        new()
        {
            InstanceId = Guid.NewGuid(),
            ServiceName = registration.ServiceName,
            ServiceVersion = registration.ServiceVersion,
            BuildVersion = registration.BuildVersion,
            Healthy = healthy,
            RegisteredUtc = registration.RegisteredUtc,
            LastHeartbeatUtc = lastHeartbeat,
        };
}
