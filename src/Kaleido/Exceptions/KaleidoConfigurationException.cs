namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when required Kaleido configuration is missing or invalid at startup.
/// This indicates a misconfigured DI registration or missing attribute, not a runtime user error.
/// The <see cref="Code"/> is a stable machine-readable diagnostic code (see <see cref="ConfigurationErrorCodes"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class KaleidoConfigurationException : Exception
{
    /// <summary>
    /// Creates the exception for missing or invalid Kaleido configuration, such as an invalid service
    /// name, an empty assembly list or conflicting authorization settings.
    /// </summary>
    /// <param name="code">
    /// The diagnostic code, a <see cref="ConfigurationErrorCodes"/> value or a code from the
    /// area's own catalog.
    /// </param>
    /// <param name="message">
    /// A description of what is misconfigured and how to fix it. Keep it free of sensitive data:
    /// when thrown during a request, the HTTP exception handler writes it to the 500 response body.
    /// </param>
    public KaleidoConfigurationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Creates the exception for missing or invalid Kaleido configuration, wrapping the exception
    /// that revealed it.
    /// </summary>
    /// <param name="code">
    /// The diagnostic code, a <see cref="ConfigurationErrorCodes"/> value or a code from the
    /// area's own catalog.
    /// </param>
    /// <param name="message">
    /// A description of what is misconfigured and how to fix it. Keep it free of sensitive data:
    /// when thrown during a request, the HTTP exception handler writes it to the 500 response body.
    /// </param>
    /// <param name="innerException">The exception that revealed the misconfiguration.</param>
    public KaleidoConfigurationException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable diagnostic code (see <see cref="ConfigurationErrorCodes"/>). Logged; when thrown during a request, the HTTP exception handler also writes it with <see cref="Exception.Message"/> to the 500 response body.</summary>
    public string Code { get; }
}

/// <summary>
/// Stable machine-readable diagnostic codes for core Kaleido configuration errors
/// (service setup and authorization settings). They are raised at startup, so they never
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

    /// <summary>A <c>Kaleido:Events</c> setting is out of range (e.g. a non-positive batch size or timeout).</summary>
    public const string InvalidEventOptions = "invalid_event_options";

    /// <summary>
    /// A configured value in the <c>Kaleido</c> section cannot be read as its setting's type (e.g. text
    /// where a number or a <see cref="TimeSpan"/> is expected, or an unknown enum name).
    /// </summary>
    public const string InvalidOptions = "invalid_options";

    /// <summary>
    /// An HTTP transport setting (<c>Kaleido:Http</c>, <c>Kaleido:Http:Map</c>, or the options passed to
    /// <c>AddHttp</c>/<c>MapKaleidoHttp</c>) is out of range or cannot be read, e.g. a non-positive
    /// idempotency lease.
    /// </summary>
    public const string InvalidHttpOptions = "invalid_http_options";

    /// <summary>
    /// <c>AddKaleido</c> or <c>AddHttp</c> was called a second time on the same service collection. A host
    /// runs one Kaleido service: configure it in one call (a second call would otherwise silently replace
    /// the first one's options).
    /// </summary>
    public const string DuplicateRegistration = "duplicate_registration";

    /// <summary>
    /// Two outbox choices were registered: a service has one - Kaleido's built-in outbox (with its
    /// storage, <c>AddOutboxStore</c>), the host's own (<c>AddOutbox</c>) or direct publication
    /// (<c>AddDirectPublication</c>) - and one storage for the built-in outbox (ADR 0023).
    /// </summary>
    public const string ConflictingOutbox = "conflicting_outbox";

    /// <summary>
    /// Two different implementations were registered for one store or for the transactions (e.g.
    /// <c>AddSqliteContextStore</c> and <c>AddContextStore&lt;T&gt;()</c>): each has one implementation
    /// per service.
    /// </summary>
    public const string ConflictingStore = "conflicting_store";

    /// <summary>
    /// The service would keep its process state, idempotency records or outbox in memory without having
    /// chosen to: a store the service uses was left at its in-memory default. Raised when the host
    /// starts. Register a durable store (e.g. <c>AddSqliteContextStore</c>, <c>AddContextStore&lt;T&gt;()</c>),
    /// or choose memory explicitly with <c>AddInMemoryStores()</c> (development, tests) (ADR 0028).
    /// </summary>
    public const string StoresNotChosen = "stores_not_chosen";
}
