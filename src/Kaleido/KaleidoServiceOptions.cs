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
    /// Must be non-empty, lowercase, and contain no whitespace or path separators.
    /// </summary>
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
    public Assembly[]? Assemblies { get; init; }

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
    /// <see cref="KaleidoAuthorizationMode.None"/> (nothing enforced).
    /// Production deployments should use
    /// <see cref="KaleidoAuthorizationMode.ZeroTrust"/> on every host,
    /// including registry aggregators/routers.
    /// </summary>
    public KaleidoAuthorizationMode AuthorizationMode { get; init; }

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

        if (options.Assemblies is null || options.Assemblies.Length == 0)
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

    // The service name is a route segment and the registry identity: non-empty, lowercase, no
    // whitespace or path separators.
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

        if (serviceName.Contains('/', StringComparison.Ordinal) || serviceName.Contains('\\', StringComparison.Ordinal))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceName,
                $"KaleidoServiceOptions.ServiceName '{serviceName}' must not contain path separators.");
        }

        if (serviceName.Any(char.IsWhiteSpace))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceName,
                $"KaleidoServiceOptions.ServiceName '{serviceName}' must not contain whitespace.");
        }

        if (serviceName.Any(char.IsUpper))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceName,
                $"KaleidoServiceOptions.ServiceName '{serviceName}' must be lowercase. " +
                $"Use '{serviceName.ToLowerInvariant()}' instead.");
        }
    }
}

/// <summary>
/// Mutable bind/configure target for <see cref="KaleidoServiceOptions"/>.
/// Configuration binding requires settable properties; <c>AddKaleido</c> binds
/// this builder, applies the configure delegate, then snapshots an immutable
/// <see cref="KaleidoServiceOptions"/> into DI. Post-registration mutation is
/// impossible — resolve the options singleton to read, never to write.
/// </summary>
public sealed class KaleidoServiceOptionsBuilder
{
    /// <inheritdoc cref="KaleidoServiceOptions.ServiceName"/>
    public string ServiceName { get; set; } = string.Empty;

    /// <inheritdoc cref="KaleidoServiceOptions.DisplayName"/>
    public string? DisplayName { get; set; }

    /// <inheritdoc cref="KaleidoServiceOptions.Description"/>
    public string? Description { get; set; }

    /// <inheritdoc cref="KaleidoServiceOptions.InstanceId"/>
    public Guid InstanceId { get; set; } = Guid.NewGuid();

    /// <inheritdoc cref="KaleidoServiceOptions.Assemblies"/>
    public Assembly[]? Assemblies { get; set; }

    /// <inheritdoc cref="KaleidoServiceOptions.IsEntryProcessor"/>
    public bool IsEntryProcessor { get; set; }

    /// <inheritdoc cref="KaleidoServiceOptions.TypeFilter"/>
    public Func<Type, bool>? TypeFilter { get; set; }

    /// <inheritdoc cref="KaleidoServiceOptions.AuthorizationMode"/>
    public KaleidoAuthorizationMode AuthorizationMode { get; set; }

    /// <inheritdoc cref="KaleidoServiceOptions.DefaultAuthorization"/>
    public AuthorizationMetadata DefaultAuthorization { get; set; } = new(Policy: null, []);

    /// <inheritdoc cref="KaleidoServiceOptions.Events"/>
    public KaleidoEventOptions Events { get; set; } = new();

    internal KaleidoServiceOptions Build() =>
        new()
        {
            ServiceName = ServiceName,
            DisplayName = DisplayName,
            Description = Description,
            InstanceId = InstanceId,
            Assemblies = Assemblies,
            IsEntryProcessor = IsEntryProcessor,
            TypeFilter = TypeFilter,
            AuthorizationMode = AuthorizationMode,
            Events = Events?.Copy() ?? throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidEventOptions,
                "Kaleido:Events must not be null."),
            DefaultAuthorization = DefaultAuthorization is { Roles: not null } declaration
                ? declaration with { Roles = [.. declaration.Roles] }
                : throw new KaleidoConfigurationException(
                    ConfigurationErrorCodes.ConflictingAuthorization,
                    "DefaultAuthorization and its Roles must not be null."),
        };
}
