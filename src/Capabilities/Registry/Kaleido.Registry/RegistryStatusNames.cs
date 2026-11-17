namespace Kaleido.Registry;

/// <summary>Stable published tokens for a service version's availability.</summary>
public static class RegistryStatusNames
{
    /// <summary>No central availability is reported.</summary>
    public const string NotReported = "notReported";

    /// <summary>Every instance is ready.</summary>
    public const string Available = "available";

    /// <summary>Some instances are ready.</summary>
    public const string Degraded = "degraded";

    /// <summary>No instance is ready but the authority has not yet heard from all instances.</summary>
    public const string Unknown = "unknown";

    /// <summary>No instance is ready.</summary>
    public const string Unavailable = "unavailable";
}

/// <summary>Stable published tokens for an individual registered instance.</summary>
public static class RegistryInstanceStateNames
{
    /// <summary>The authority has not yet heard from this instance since resuming.</summary>
    public const string Unknown = "unknown";

    /// <summary>The instance is healthy and has a current lease.</summary>
    public const string Ready = "ready";

    /// <summary>The instance is unhealthy or its lease expired.</summary>
    public const string NotReady = "notReady";
}
