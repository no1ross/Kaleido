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
    /// Optional filter to control which process steps are registered.
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

    public AuthorizationMetadata DefaultAuthorization { get; init; } = AuthorizationMetadata.Unspecified;

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

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceName,
                "KaleidoServiceOptions.ServiceName must be a non-empty string. " +
                "Set it via AddKaleido(config, o => o.ServiceName = \"my-service\") " +
                "or via the Kaleido:ServiceName configuration key.");
        }

        if (options.ServiceName.Contains('/') || options.ServiceName.Contains('\\'))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceName,
                $"KaleidoServiceOptions.ServiceName '{options.ServiceName}' must not contain path separators.");
        }

        if (options.ServiceName.Any(char.IsWhiteSpace))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceName,
                $"KaleidoServiceOptions.ServiceName '{options.ServiceName}' must not contain whitespace.");
        }

        if (options.ServiceName.Any(char.IsUpper))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidServiceName,
                $"KaleidoServiceOptions.ServiceName '{options.ServiceName}' must be lowercase. " +
                $"Use '{options.ServiceName.ToLowerInvariant()}' instead.");
        }

        if (options.Assemblies is null || options.Assemblies.Length == 0)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.MissingAssembly,
                "KaleidoServiceOptions.Assemblies must contain at least one assembly. " +
                "Set o.Assemblies in the AddKaleido configure callback.");
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

    public AuthorizationMetadata DefaultAuthorization { get; set; } = new(null, []);

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
            DefaultAuthorization = DefaultAuthorization is { Roles: not null } declaration
                ? declaration with { Roles = [.. declaration.Roles] }
                : throw new KaleidoConfigurationException(
                    ConfigurationErrorCodes.ConflictingAuthorization,
                    "DefaultAuthorization and its Roles must not be null.")
        };
}
