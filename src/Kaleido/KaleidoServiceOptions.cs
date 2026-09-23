using System.Reflection;

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
    /// produces <c>/intake/processes/...</c> and <c>/intake/queries/...</c>).
    /// Must be non-empty, lowercase, and contain no whitespace or path separators.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable display name for this service (e.g. <c>"Prior Auth Intake"</c>).
    /// Optional. Surfaced in registry responses.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// A description of this service's purpose. Optional. Surfaced in registry responses.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// A stable unique identifier for this running instance of the service.
    /// Generated fresh at startup by default. Propagated via the
    /// <c>X-Kaleido-Processor-Instance-Id</c> correlation header for auditing and tracing.
    /// </summary>
    public Guid InstanceId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The assemblies to scan for Process and Queryable registrations.
    /// If null or empty, defaults to Assembly.GetCallingAssembly() and Assembly.GetEntryAssembly().
    /// </summary>
    public Assembly[]? Assemblies { get; set; }

    /// <summary>
    /// Marks this processor as the entry point for the application workflow.
    /// When true, the registry will identify this processor as the one consumers
    /// should start with. Only one processor in a distributed system should have
    /// this set to true.
    /// </summary>
    public bool IsEntryProcessor { get; set; }

    /// <summary>
    /// Optional filter to control which process steps are registered.
    /// Useful when sharing assemblies across multiple services.
    /// </summary>
    public Func<Type, bool>? TypeFilter { get; set; }

    /// <summary>
    /// Validates a <see cref="KaleidoServiceOptions"/> instance.
    /// Throws <see cref="KaleidoConfigurationException"/> if <see cref="ServiceName"/> is null,
    /// empty, contains whitespace, path separators, or uppercase characters.
    /// </summary>
    /// <param name="options">The options instance to validate.</param>
    /// <exception cref="KaleidoConfigurationException">
    /// Thrown when <see cref="ServiceName"/> fails any validation rule.
    /// </exception>
    public static void Validate(KaleidoServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

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
    }
}
