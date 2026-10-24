using System.Reflection;
using Kaleido.Registry;

namespace Kaleido;

/// <summary>
/// Service-level identity and configuration for a Kaleido-enabled application.
/// Registered as a singleton by <c>AddKaleido</c>.
/// Consumed by Process, Queryable, and Registry subsystems to derive route prefixes,
/// populate registry responses, and propagate instance identity via correlation headers.
/// </summary>
public class KaleidoServiceOptions
{
    /// <summary>
    /// The configuration section name shared by all Kaleido subsystem options.
    /// </summary>
    public const string SectionName = "Kaleido";

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
    /// A read-only copy taken when the options are built: changing the array assigned to
    /// <see cref="KaleidoServiceOptionsBuilder.Assemblies"/> afterwards does not change it.
    /// </remarks>
    public IReadOnlyList<Assembly>? Assemblies { get; init; }

    /// <summary>
    /// Marks this processor as the entry point for the application workflow.
    /// When true, the registry will identify this processor as the one consumers
    /// should start with. Only one processor in a distributed system should have
    /// this set to true.
    /// </summary>
    public bool IsEntryProcessor { get; init; }

    /// <summary>
    /// Optional filter to control which step and query source/view types are registered.
    /// Useful when sharing assemblies across multiple services.
    /// </summary>
    public Func<Type, bool>? TypeFilter { get; init; }

    /// <summary>
    /// How capability authorization is enforced. Default
    /// <see cref="KaleidoAuthorizationMode.Authenticated"/>: every capability requires an
    /// authenticated caller, so a host must register authentication, or choose
    /// <see cref="KaleidoAuthorizationMode.None"/> explicitly to leave its capabilities open
    /// (ADR 0021). Production deployments should use
    /// <see cref="KaleidoAuthorizationMode.ZeroTrust"/> on every host, including registry
    /// aggregators/routers.
    /// </summary>
    public KaleidoAuthorizationMode AuthorizationMode { get; init; } = KaleidoAuthorizationMode.Authenticated;

    /// <summary>
    /// The service-wide authorization rule for process steps and query sources that declare no
    /// <see cref="KaleidoAuthorizationAttribute"/> of their own; query views without one inherit
    /// their source's rule. Default <see cref="AuthorizationMetadata.Unspecified"/> (no service rule).
    /// Takes effect only when <see cref="AuthorizationMode"/> enforces authorization; in
    /// <see cref="KaleidoAuthorizationMode.ZeroTrust"/> an explicit default makes every capability
    /// explicit.
    /// </summary>
    /// <remarks>
    /// <see cref="AuthorizationMetadata.Roles"/> must not be <see langword="null"/>, and
    /// <see cref="AuthorizationMetadata.AllowAnonymous"/> cannot be combined with roles or a policy;
    /// <c>AddKaleido</c> throws a <see cref="KaleidoConfigurationException"/> otherwise.
    /// </remarks>
    public AuthorizationMetadata DefaultAuthorization { get; init; } = AuthorizationMetadata.Unspecified;

    /// <summary>
    /// How the event relay hands outbox entries to the event publisher (<c>Kaleido:Events</c>):
    /// batch size, parallelism and resilience. See <see cref="KaleidoEventOptions"/>.
    /// </summary>
    public KaleidoEventOptions Events { get; init; } = new();

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

        ValidateAuthorization(options);
        ValidateServiceName(options.ServiceName);
        KaleidoEventOptions.Validate(options.Events);

        if (options.Assemblies is null || options.Assemblies.Count == 0)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.MissingAssembly,
                "KaleidoServiceOptions.Assemblies must contain at least one assembly. " +
                "Set o.Assemblies in the AddKaleido configure callback.");
        }
    }

    private static void ValidateAuthorization(KaleidoServiceOptions options)
    {
        if (!Enum.IsDefined(options.AuthorizationMode))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidAuthorizationMode,
                $"AuthorizationMode '{options.AuthorizationMode}' is not supported. Use None, Authenticated, or ZeroTrust.");
        }

        if (options.DefaultAuthorization is null
            || options.DefaultAuthorization.Roles is null)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ConflictingAuthorization,
                "DefaultAuthorization and its Roles must not be null. Use AuthorizationMetadata.Unspecified when no service default is declared.");
        }

        if (options.DefaultAuthorization.AllowAnonymous
            && (options.DefaultAuthorization.Roles.Count > 0
                || !string.IsNullOrWhiteSpace(options.DefaultAuthorization.Policy)))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ConflictingAuthorization,
                "DefaultAuthorization cannot combine AllowAnonymous with Roles or Policy.");
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
                "Set it via AddKaleido(config, o => o.ServiceName = \"myservice\") " +
                "or via the Kaleido:ServiceName configuration key.");
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

    // The service-name rule, shared with the transports: a client's route prefix must equal a service
    // name, so Kaleido.Http.Client validates RoutePrefix with it (#434).
    internal static bool IsValidServiceName(string? value) =>
        !string.IsNullOrEmpty(value)
        && IsLowercaseLetterOrDigit(value[0])
        && value.All(c => IsLowercaseLetterOrDigit(c) || c is '-' or '_');

    private static bool IsLowercaseLetterOrDigit(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
