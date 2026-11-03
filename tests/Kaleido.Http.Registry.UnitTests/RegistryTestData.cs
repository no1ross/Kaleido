namespace Kaleido.Http.Registry.UnitTests;

// Registrations, instances and capabilities for the registry's tests.
internal static class RegistryTestData
{
    public static readonly DateTimeOffset Start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    public static ProcessorRegistryResponse ProcessorWith(string service, params string[] steps) =>
        new()
        {
            Name = service,
            ServiceName = service,
            InitialSteps = [.. steps.Select(x => new ProcessStepSummary { Name = x })],
            Steps = [.. steps.Select(x => new ProcessStepResponse { Name = x })],
        };

    public static QueryableSourceResponse SourceWith(string service, string name, params string[] views) =>
        new()
        {
            ServiceName = service,
            Name = name,
            Views = [.. views.Select(x => new QueryableViewResponse { Name = x, QueryUrl = $"/{service}/queryable/{name}/views/{x}" })],
        };

    // One build's registry entry: its processes and query sources.
    public static KaleidoRegistry Entry(IReadOnlyCollection<ProcessorRegistryResponse> processes, IReadOnlyCollection<QueryableSourceResponse> queryables) =>
        new() { Processes = processes, Queryables = queryables };

    // A registration and its entry's serialized form, as the store keeps them.
    public static (RegistryRegistrationRecord Record, string Capabilities) Registration(string service, string version, KaleidoRegistry entry, DateTimeOffset? registered = null)
    {
        var json = entry.ToEntryJson();

        return (
            new RegistryRegistrationRecord
            {
                ServiceName = service,
                ServiceVersion = version,
                Revision = json.ToRevision(),
                RegisteredUtc = registered ?? Start,
            },
            json);
    }

    public static RegistryInstanceRecord Instance(RegistryRegistrationRecord registration, DateTimeOffset lastHeartbeat, bool healthy = true) =>
        new()
        {
            InstanceId = Guid.NewGuid(),
            ServiceName = registration.ServiceName,
            ServiceVersion = registration.ServiceVersion,
            Revision = registration.Revision,
            Healthy = healthy,
            RegisteredUtc = registration.RegisteredUtc,
            LastHeartbeatUtc = lastHeartbeat,
        };
}
