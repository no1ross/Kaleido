using System.Reflection;
using Kaleido.Registry;

namespace Kaleido;

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

    /// <summary>
    /// The assemblies to scan for Process and Queryable registrations.
    /// A non-empty explicit list is required when calling AddKaleido().
    /// </summary>
    /// <remarks>
    /// Copied into a read-only <see cref="KaleidoServiceOptions.Assemblies"/> when the options are
    /// built; later changes to this array have no effect.
    /// </remarks>
    public Assembly[]? Assemblies { get; set; }

    /// <inheritdoc cref="KaleidoServiceOptions.IsEntryProcessor"/>
    public bool IsEntryProcessor { get; set; }

    /// <inheritdoc cref="KaleidoServiceOptions.TypeFilter"/>
    public Func<Type, bool>? TypeFilter { get; set; }

    /// <inheritdoc cref="KaleidoServiceOptions.AuthorizationMode"/>
    public KaleidoAuthorizationMode AuthorizationMode { get; set; } = KaleidoAuthorizationMode.Authenticated;

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
            Assemblies = Assemblies is null ? null : Array.AsReadOnly([.. Assemblies]),
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
