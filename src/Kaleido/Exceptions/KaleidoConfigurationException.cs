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
/// Queryable-specific codes are prefixed <c>qry_</c>; Process-specific codes are prefixed <c>pro_</c>.
/// Cross-cutting codes have no prefix.
/// </summary>
public static class ConfigurationErrorCodes
{
    // Generic / cross-cutting (no prefix)

    /// <summary>KaleidoServiceOptions.ServiceName is null, empty, or contains invalid characters.</summary>
    public const string InvalidServiceName      = "invalid_service_name";

    /// <summary>At least one assembly must be registered before calling AddQueryable() or AddProcessor().</summary>
    public const string MissingAssembly         = "missing_assembly";

    // Process-specific (pro_ prefix)

    /// <summary>A process step type is missing the [ProcessStep] attribute.</summary>
    public const string ProMissingAttribute     = "pro_missing_attribute";

    /// <summary>A process step has no registered handler.</summary>
    public const string ProMissingHandler       = "pro_missing_handler";

    /// <summary>A registered handler does not implement a valid IProcessStepHandler interface.</summary>
    public const string ProInvalidHandler       = "pro_invalid_handler";

    /// <summary>Duplicate process step names were detected across the registered assemblies.</summary>
    public const string ProDuplicateStep        = "pro_duplicate_step";

    /// <summary>A process step registration is structurally invalid (e.g. self-reference, circular dependency).</summary>
    public const string ProInvalidRegistration  = "pro_invalid_registration";

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
