using System.Net;

namespace Kaleido.Http.Client;

/// <summary>
/// Thrown when a remote Kaleido service call fails (HTTP error, missing payload, or unknown registration).
/// The <see cref="Code"/> is a stable machine-readable diagnostic code (see <see cref="HttpClientErrorCodes"/>).
/// Carries the <see cref="StatusCode"/> of the response and any structured <see cref="Errors"/> from the error body.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class KaleidoHttpClientException : Exception
{
    /// <summary>
    /// Creates the exception for a failed remote Kaleido call.
    /// </summary>
    /// <param name="code">The diagnostic code, a <see cref="HttpClientErrorCodes"/> value.</param>
    /// <param name="message">A description of the call and why it failed.</param>
    /// <param name="statusCode">
    /// The status of the remote response, or the status that best describes a failure detected by
    /// the client itself (e.g. <see cref="HttpStatusCode.NotFound"/> for a name missing from the
    /// remote registry).
    /// </param>
    /// <param name="errors">
    /// The structured errors from the remote error body; <see langword="null"/> becomes an empty list.
    /// </param>
    public KaleidoHttpClientException(
        string code,
        string message,
        HttpStatusCode statusCode,
        IReadOnlyList<KaleidoError>? errors = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    /// <summary>
    /// Creates the exception for a failed remote Kaleido call, wrapping the exception that caused it.
    /// </summary>
    /// <param name="code">The diagnostic code, a <see cref="HttpClientErrorCodes"/> value.</param>
    /// <param name="message">A description of the call and why it failed.</param>
    /// <param name="statusCode">
    /// The status of the remote response, or the status that best describes a failure detected by
    /// the client itself.
    /// </param>
    /// <param name="innerException">The exception that caused the failure.</param>
    /// <param name="errors">
    /// The structured errors from the remote error body; <see langword="null"/> becomes an empty list.
    /// </param>
    public KaleidoHttpClientException(
        string code,
        string message,
        HttpStatusCode statusCode,
        Exception innerException,
        IReadOnlyList<KaleidoError>? errors = null)
        : base(message, innerException)
    {
        Code = code;
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    /// <summary>Stable machine-readable diagnostic code (see <see cref="HttpClientErrorCodes"/>).</summary>
    public string Code { get; }

    /// <summary>
    /// The status of the remote response, e.g. <see cref="HttpStatusCode.BadRequest"/> when the
    /// service rejected the request or <see cref="HttpStatusCode.Conflict"/> for <see cref="HttpClientErrorCodes.Conflict"/>.
    /// When the client detected the failure itself, the status that best describes it, e.g.
    /// <see cref="HttpStatusCode.NotFound"/> for <see cref="HttpClientErrorCodes.NotFound"/>.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// The structured errors from the remote error body, with the called service's own codes
    /// (e.g. <c>pro_*</c>, <c>qry_*</c>); empty when the response carried none or the client
    /// detected the failure itself.
    /// </summary>
    public IReadOnlyList<KaleidoError> Errors { get; }
}

/// <summary>
/// Stable machine-readable diagnostic codes for Kaleido HTTP client errors. Runtime codes are
/// prefixed <c>httpclient_</c>; <c>missing_base_url</c> is a startup configuration code. The
/// called service's own codes (<c>pro_*</c>, <c>qry_*</c>) arrive in
/// <see cref="KaleidoHttpClientException.Errors"/>.
/// </summary>
public static class HttpClientErrorCodes
{
    /// <summary>A required registration (context, view, or step) was not found in the remote registry.</summary>
    public const string NotFound = "httpclient_not_found";

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

    /// <summary>A URL returned by a remote registry was not a valid absolute http(s) URL.</summary>
    public const string InvalidRegistryUrl = "httpclient_invalid_registry_url";

    /// <summary>A configured Kaleido client is missing a BaseUrl (neither client-level nor shared Kaleido:BaseUrl). Raised at startup as a configuration error.</summary>
    public const string MissingBaseUrl = "missing_base_url";
}
