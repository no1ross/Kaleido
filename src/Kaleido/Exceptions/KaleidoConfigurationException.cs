namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when required Kaleido configuration is missing or invalid at startup.
/// This indicates a misconfigured DI registration or missing attribute, not a runtime user error.
/// The <see cref="Code"/> is a stable machine-readable diagnostic code (see <see cref="ConfigurationErrorCodes"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class KaleidoConfigurationException : Exception
{
    public KaleidoConfigurationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public KaleidoConfigurationException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable diagnostic code (see <see cref="ConfigurationErrorCodes"/>). Log-only — not forwarded to HTTP responses.</summary>
    public string Code { get; }
}

/// <summary>
/// Stable machine-readable diagnostic codes for core Kaleido configuration errors
/// (service setup and authorization settings). These are log-only — startup crashes never
/// reach HTTP response bodies. Each area owns its own startup codes:
/// <see cref="Kaleido.Queryable.QueryableErrorCodes"/>, <see cref="Kaleido.Processor.ProcessorErrorCodes"/>,
/// and the transport/provider projects' own catalogs.
/// </summary>
public static class ConfigurationErrorCodes
{
    /// <summary>KaleidoServiceOptions.ServiceName is null, empty, or contains invalid characters.</summary>
    public const string InvalidServiceName = "invalid_service_name";

    /// <summary>AddKaleido requires a non-empty explicit assembly list before registering services.</summary>
    public const string MissingAssembly = "missing_assembly";

    /// <summary>A [KaleidoAuthorization] declares AllowAnonymous together with Roles or Policy.</summary>
    public const string ConflictingAuthorization = "conflicting_authorization";

    /// <summary>Authorization is enforced (AuthorizationMode is not None) but the host has no authentication scheme registered.</summary>
    public const string AuthenticationNotConfigured = "authentication_not_configured";

    /// <summary>KaleidoServiceOptions.AuthorizationMode is not a defined value.</summary>
    public const string InvalidAuthorizationMode = "invalid_authorization_mode";
}
