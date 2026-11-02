namespace Kaleido.Http.Registry;

// The control registry's endpoint names (#553).
internal static class ControlRegistryEndpointNames
{
    public const string Register = "KaleidoRegistryRegister";
    public const string Heartbeat = "KaleidoRegistryHeartbeat";
    public const string Deregister = "KaleidoRegistryDeregister";
    public const string Read = "KaleidoRegistryRead";
    public const string Status = "KaleidoRegistryStatus";
}
