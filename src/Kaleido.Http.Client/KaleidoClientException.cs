using System.Net;

namespace Kaleido.Http.Client;

/// <summary>
/// Thrown when a remote Kaleido service call fails (HTTP error, missing payload, or unknown registration).
/// The <see cref="Code"/> is a stable machine-readable diagnostic code (see <see cref="HttpClientErrorCodes"/>).
/// Carries the <see cref="StatusCode"/> of the response and any structured <see cref="Errors"/> from the error body.
/// </summary>
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
/// Stable machine-readable diagnostic codes for Kaleido HTTP client errors.
/// Process-specific codes are prefixed <c>pro_</c>; Queryable-specific codes are prefixed <c>qry_</c>.
/// Cross-cutting codes have no prefix.
/// </summary>
public static class HttpClientErrorCodes
{
    /// <summary>A required registration (context, view, or step) was not found in the remote registry.</summary>
    public const string NotFound             = "httpclient_not_found";

    /// <summary>A remote request succeeded but returned an empty payload.</summary>
    public const string EmptyResponse        = "httpclient_empty_response";

    /// <summary>A remote request failed with a non-success HTTP status code.</summary>
    public const string RequestFailed        = "httpclient_request_failed";

    /// <summary>A remote queryable request failed with structured validation errors.</summary>
    public const string ValidationFailed     = "httpclient_validation_failed";
}
