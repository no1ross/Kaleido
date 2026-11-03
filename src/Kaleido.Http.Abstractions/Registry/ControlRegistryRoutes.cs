namespace Kaleido.Http.Registry;

// The control registry's routes (#553), shared by its endpoints and by the participants that register
// with it and read it: the templates inside its /registry group, and the concrete URLs.
internal static class ControlRegistryRoutePaths
{
    public const string Group = "/registry";

    public const string Read = "";

    public const string Status = "/status";

    public const string Capabilities = "/capabilities";

    public const string Instances = "/instances";

    public const string Instance = "/instances/{instanceId:guid}";

    public const string Heartbeat = "/instances/{instanceId:guid}/heartbeat";
}

internal static class ControlRegistryContractUrls
{
    public const string Registry = ControlRegistryRoutePaths.Group;

    public const string Status = ControlRegistryRoutePaths.Group + ControlRegistryRoutePaths.Status;

    public const string Capabilities = ControlRegistryRoutePaths.Group + ControlRegistryRoutePaths.Capabilities;

    public const string Instances = ControlRegistryRoutePaths.Group + ControlRegistryRoutePaths.Instances;

    public static string Instance(Guid instanceId) => $"{Instances}/{instanceId:D}";

    public static string Heartbeat(Guid instanceId) => $"{Instance(instanceId)}/heartbeat";
}

internal static class ControlRegistryEndpointNames
{
    public const string Register = "KaleidoRegistryRegister";
    public const string Heartbeat = "KaleidoRegistryHeartbeat";
    public const string Deregister = "KaleidoRegistryDeregister";
    public const string Read = "KaleidoRegistryRead";
    public const string Status = "KaleidoRegistryStatus";
    public const string Capabilities = "KaleidoRegistryCapabilities";
}
