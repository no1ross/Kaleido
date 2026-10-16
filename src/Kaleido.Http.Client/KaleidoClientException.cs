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

    public HttpStatusCode StatusCode { get; }

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
