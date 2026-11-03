namespace Kaleido.Provider.SQLite.Entities;

// The control registry's tables (#553). Times are UTC ticks; ids are Guid "N" strings.

// One build (revision) of a service version, with its capabilities as the registry serialized them.
internal sealed class RegistryRegistrationEntity
{
    public string Revision { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string ServiceVersion { get; set; } = string.Empty;

    public string Capabilities { get; set; } = string.Empty;

    public long RegisteredUtc { get; set; }
}

// An instance of a service version and its last heartbeat.
internal sealed class RegistryInstanceEntity
{
    public string InstanceId { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string ServiceVersion { get; set; } = string.Empty;

    public string Revision { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool Healthy { get; set; }

    public long RegisteredUtc { get; set; }

    public long LastHeartbeatUtc { get; set; }
}

// A service's current version.
internal sealed class RegistryCurrentVersionEntity
{
    public string ServiceName { get; set; } = string.Empty;

    public string ServiceVersion { get; set; } = string.Empty;
}
