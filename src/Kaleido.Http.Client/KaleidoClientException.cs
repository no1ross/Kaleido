using System.Net;

namespace Kaleido.Http.Client;

/// <summary>
/// Thrown when a remote Kaleido service call fails (HTTP error or missing payload).
/// The <see cref="Code"/> is a stable machine-readable diagnostic code (see <see cref="HttpClientErrorCodes"/>).
/// Carries the <see cref="StatusCode"/> of the response and any structured <see cref="Errors"/> from the error body.
/// </summary>
/// <param name="code">The diagnostic code, a <see cref="HttpClientErrorCodes"/> value.</param>
/// <param name="message">A description of the call and why it failed.</param>
/// <param name="statusCode">
/// The status of the remote response, or the status that best describes a failure detected by
/// the client itself (e.g. <see cref="HttpStatusCode.InternalServerError"/> for an empty payload).
/// </param>
/// <param name="errors">
/// The structured errors from the remote error body; <see langword="null"/> becomes an empty list.
/// </param>
/// <param name="innerException">The exception that caused the failure, if any (a timeout, a broken connection).</param>
[ExcludeFromCodeCoverage]
public sealed class KaleidoHttpClientException(
    string code,
    string message,
    HttpStatusCode statusCode,
    IReadOnlyList<KaleidoError>? errors = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Stable machine-readable diagnostic code (see <see cref="HttpClientErrorCodes"/>).</summary>
    public string Code { get; } = code;

    /// <summary>
    /// The status of the remote response, e.g. <see cref="HttpStatusCode.BadRequest"/> when the
    /// service rejected the request or <see cref="HttpStatusCode.Conflict"/> for <see cref="HttpClientErrorCodes.Conflict"/>.
    /// A step, source or view the service does not have, or the caller may not use, arrives as the
    /// service's own <see cref="HttpStatusCode.NotFound"/> or <see cref="HttpStatusCode.Forbidden"/>
    /// with <see cref="HttpClientErrorCodes.RequestFailed"/>.
    /// </summary>
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>
    /// The structured errors from the remote error body, with the called service's own codes
    /// (e.g. <c>pro_*</c>, <c>qry_*</c>); empty when the response carried none or the client
    /// detected the failure itself.
    /// </summary>
    public IReadOnlyList<KaleidoError> Errors { get; } = errors ?? [];
}

/// <summary>
/// Stable machine-readable diagnostic codes for Kaleido HTTP client errors. Runtime codes are
/// prefixed <c>httpclient_</c>; <c>missing_base_url</c> is a startup configuration code. The
/// called service's own codes (<c>pro_*</c>, <c>qry_*</c>) arrive in
/// <see cref="KaleidoHttpClientException.Errors"/>.
/// </summary>
public static class HttpClientErrorCodes
{
    /// <summary>A remote request succeeded but returned an empty payload.</summary>
    public const string EmptyResponse = "httpclient_empty_response";

    /// <summary>A remote request failed with a non-success HTTP status code.</summary>
    public const string RequestFailed = "httpclient_request_failed";

    /// <summary>A remote queryable request failed with structured validation errors.</summary>
    public const string ValidationFailed = "httpclient_validation_failed";

    /// <summary>
    /// The remote process was changed by another request since this one read it (HTTP 409,
    /// <c>pro_process_conflict</c> in the errors): nothing was saved. Read the process state again and
    /// decide on the next step instead of repeating the call.
    /// </summary>
    public const string Conflict = "httpclient_conflict";

    /// <summary>A configured Kaleido client is missing a BaseUrl (neither client-level nor shared Kaleido:BaseUrl). Raised at startup as a configuration error.</summary>
    public const string MissingBaseUrl = "missing_base_url";

    /// <summary>
    /// An <see cref="IKaleidoCallCredentialProvider"/> answered <see cref="KaleidoCallCredential.Configured"/>
    /// for a call made for a caller; a caller's call never goes out with the host's own credentials.
    /// Raised on the call as a configuration error.
    /// </summary>
    public const string ConfiguredCredentialForCaller = "httpclient_configured_credential_for_caller";

    /// <summary>
    /// <c>GetClient</c> was asked for a client name that is not configured under <c>Kaleido:Clients</c>.
    /// Raised as a configuration error at the call; the message lists the configured clients.
    /// </summary>
    public const string UnknownClient = "httpclient_unknown_client";
}
