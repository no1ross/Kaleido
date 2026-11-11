using System.Reflection;

namespace Kaleido;

/// <summary>Startup options bound from their own Kaleido configuration path and validated before registration.</summary>
public interface IKaleidoOptions
{
    /// <summary>The complete configuration path owned by these options, such as <c>Kaleido:Events</c>.</summary>
    string ConfigurationPath { get; }

    /// <summary>Validates the effective values after configuration is laid over a copy of code options.</summary>
    void Validate();
}

/// <summary>
/// Service-level identity and configuration for a Kaleido-enabled application.
/// Immutable: <c>AddKaleido</c> takes the host's instance, lays the <c>Kaleido</c> configuration section
/// over a copy of it (configuration overrides code, setting by setting), validates it and registers it
/// as a singleton (ADR 0030).
/// Consumed by Process, Queryable and Registry subsystems to derive route prefixes and
/// populate registry responses. Instance identity is local and is never read from a header.
/// </summary>
public sealed record KaleidoServiceOptions : IKaleidoOptions
{
    string IKaleidoOptions.ConfigurationPath => "Kaleido";

    void IKaleidoOptions.Validate() => Validate(this);

    /// <summary>
    /// The unique name identifying this service within the distributed system.
    /// Used as the route prefix for Process and Queryable endpoints (e.g. <c>"intake"</c>
    /// produces <c>/intake/processes/...</c> and <c>/intake/queryable/...</c>).
    /// Must start with a lowercase ASCII letter or digit and contain only lowercase ASCII letters,
    /// digits, <c>-</c> and <c>_</c> (for example <c>intake</c> or <c>intake-events</c>), so it
    /// is a safe URL path segment and MCP tool-name prefix.
    /// </summary>
    /// <remarks>
    /// <c>AddKaleido</c> rejects any other value with a <see cref="KaleidoConfigurationException"/>
    /// (<c>invalid_service_name</c>). The consumer analyzer KAL2005 reports the same rule at build time
    /// for constant values; a value read from configuration is checked only at startup.
    /// </remarks>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// The version of this service, when versions of it run side by side (for example <c>"2"</c> or
    /// <c>"2026-11"</c>): an opaque label Kaleido never compares or orders. <see langword="null"/> (the
    /// default) means the service is unversioned.
    /// </summary>
    /// <remarks>
    /// Every record Kaleido stores is scoped by the service and this version (#552): a process belongs to
    /// the version that created it, so a process of version <c>1</c> is not found by version <c>2</c>, and
    /// services and versions may share one database. It must start with an ASCII letter or digit, contain
    /// only ASCII letters, digits, <c>.</c>, <c>-</c> and <c>_</c>, and be at most
    /// <see cref="MaxServiceVersionLength"/> characters; <c>AddKaleido</c> rejects any other value
    /// (<c>invalid_service_version</c>). Set it in code or with the <c>Kaleido:ServiceVersion</c>
    /// configuration key.
    /// </remarks>
    public string? ServiceVersion { get; init; }

    /// <summary>The longest <see cref="ServiceVersion"/> accepted.</summary>
    public const int MaxServiceVersionLength = 64;

    // The service and its version as one name, for keys of stores shared by services and versions (the
    // registry snapshots, #552): "orders", or "orders@2". Neither a service name nor a version can
    // contain '@', so no two read the same.
    internal string ScopeName => ServiceVersion is null ? ServiceName : $"{ServiceName}@{ServiceVersion}";

    /// <summary>
    /// A human-readable display name for this service (e.g. <c>"Prior Auth Intake"</c>).
    /// Optional. Surfaced in registry responses.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// A description of this service's purpose. Optional. Surfaced in registry responses.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// A stable unique identifier for this running instance of the service.
    /// Generated fresh at startup by default. Recorded in this service's own trace tags,
    /// events and logs for auditing; never sent or read in correlation headers.
    /// </summary>
    public Guid InstanceId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The assemblies to scan for Process and Queryable registrations.
    /// A non-empty explicit list is required when calling AddKaleido().
    /// </summary>
    /// <remarks>
    /// Set in code only (assemblies cannot come from configuration). <c>AddKaleido</c> registers a
    /// read-only copy, so changing the array passed in afterwards does not change it.
    /// </remarks>
    public IReadOnlyList<Assembly>? Assemblies { get; init; }

    /// <summary>
    /// Optional filter to control which step and query source/view types are registered.
    /// Useful when sharing assemblies across multiple services.
    /// </summary>
    public Func<Type, bool>? TypeFilter { get; init; }

    /// <summary>
    /// Validates a <see cref="KaleidoServiceOptions"/> instance.
    /// Throws <see cref="KaleidoConfigurationException"/> if <see cref="ServiceName"/> is invalid
    /// or <see cref="Assemblies"/> is null or empty.
    /// </summary>
    /// <param name="options">The options instance to validate.</param>
    /// <exception cref="KaleidoConfigurationException">
    /// Thrown when <see cref="ServiceName"/> or <see cref="Assemblies"/> fails validation.
    /// </exception>
    internal static void Validate(KaleidoServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateServiceName(options.ServiceName);
        ValidateServiceVersion(options.ServiceVersion);
        if (options.Assemblies is null || options.Assemblies.Count == 0)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.MissingAssembly,
                "KaleidoServiceOptions.Assemblies must contain at least one assembly. " +
                "Set Assemblies in the options passed to AddKaleido: new() { Assemblies = [typeof(Program).Assembly] }.");
        }
    }

    // The service name is a route segment, the registry identity and the MCP tool-name prefix:
    // [a-z0-9][a-z0-9_-]*. KAL2005 (Kaleido.Analyzers ServiceNameAnalyzer) applies the same rule;
    // both are tested against tests/Shared/ServiceNameCases.cs.
    private static void ValidateServiceName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceName,
                "KaleidoServiceOptions.ServiceName must be a non-empty string. " +
                "Set it in the options passed to AddKaleido (new() { ServiceName = \"myservice\" }) " +
                "or with the Kaleido:ServiceName configuration key.");
        }

        if (!IsValidServiceName(serviceName))
        {
            var suggestion = new string([.. serviceName.ToLowerInvariant().Where(c => IsLowercaseLetterOrDigit(c) || c is '-' or '_')]).TrimStart('-', '_');
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceName,
                $"KaleidoServiceOptions.ServiceName '{serviceName}' must start with a lowercase letter or digit " +
                "and contain only lowercase letters (a-z), digits, '-' and '_': it is the route prefix and the MCP tool-name prefix." +
                (suggestion.Length > 0 ? $" Use '{suggestion}' instead." : string.Empty));
        }
    }

    // The version is part of every store key and will be a route segment (#553): an ASCII letter or digit
    // first, then letters, digits, '.', '-' and '_'.
    private static void ValidateServiceVersion(string? serviceVersion)
    {
        if (serviceVersion is null)
        {
            return;
        }

        if (!IsValidServiceVersion(serviceVersion))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceVersion,
                $"KaleidoServiceOptions.ServiceVersion '{serviceVersion}' must be 1 to {MaxServiceVersionLength} characters, start with an ASCII letter or digit " +
                "and contain only ASCII letters, digits, '.', '-' and '_'. Leave it unset for an unversioned service.");
        }
    }

    // The service-version rule, shared with the control registry, which checks each registration (#553).
    internal static bool IsValidServiceVersion(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaxServiceVersionLength
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    // The service-name rule, shared with the transports: a client's route prefix must equal a service
    // name, so Kaleido.Http.Client validates RoutePrefix with it (#434).
    internal static bool IsValidServiceName(string? value) =>
        !string.IsNullOrEmpty(value)
        && IsLowercaseLetterOrDigit(value[0])
        && value.All(c => IsLowercaseLetterOrDigit(c) || c is '-' or '_');

    private static bool IsLowercaseLetterOrDigit(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
