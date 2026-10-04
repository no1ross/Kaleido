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
/// Stable machine-readable diagnostic codes for Kaleido configuration errors.
/// These are log-only — startup crashes never reach HTTP response bodies.
/// Queryable-specific codes are prefixed <c>qry_</c>; cross-cutting codes have no prefix.
/// Process-specific codes live in <see cref="Kaleido.Processor.ProcessorErrorCodes"/>.
/// </summary>
public static class ConfigurationErrorCodes
{
    // Generic / cross-cutting (no prefix)

    /// <summary>KaleidoServiceOptions.ServiceName is null, empty, or contains invalid characters.</summary>
    public const string InvalidServiceName      = "invalid_service_name";

    /// <summary>AddKaleido requires a non-empty explicit assembly list before registering services.</summary>
    public const string MissingAssembly         = "missing_assembly";

    /// <summary>A configured Kaleido client is missing a BaseUrl (neither client-level nor shared Kaleido:BaseUrl).</summary>
    public const string MissingBaseUrl          = "missing_base_url";

    public const string InvalidConnectionString = "invalid_connection_string";

    /// <summary>A [KaleidoAuthorization] declares AllowAnonymous together with Roles or Policy.</summary>
    public const string ConflictingAuthorization = "conflicting_authorization";

    /// <summary>Authorization is enforced (AuthorizationMode is not None) but the host has no authentication scheme registered.</summary>
    public const string AuthenticationNotConfigured = "authentication_not_configured";

    public const string InvalidAuthorizationMode = "invalid_authorization_mode";

    /// <summary>Reserved for hosts that choose to reject undeclared authorization at startup rather than omit the capability.</summary>
    public const string UndeclaredAuthorization = "undeclared_authorization";

    // Queryable-specific (qry_ prefix)

    /// <summary>A query context or view type is missing a required attribute ([QueryContext] or [QueryView]).</summary>
    public const string QryMissingAttribute     = "qry_missing_attribute";

    /// <summary>A query context has no registered IQueryContextSource or IQueryContextSourceAsync.</summary>
    public const string QryMissingSource        = "qry_missing_source";

    /// <summary>A query context has multiple registered sources.</summary>
    public const string QryDuplicateSource      = "qry_duplicate_source";

    /// <summary>Duplicate query context or view names were detected across the registered assemblies.</summary>
    public const string QryDuplicateRegistration = "qry_duplicate_registration";

    /// <summary>A query context or view registration is structurally invalid (e.g. bad sort field, unregistered context reference, invalid contract type).</summary>
    public const string QryInvalidRegistration  = "qry_invalid_registration";
}
