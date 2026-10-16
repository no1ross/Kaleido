namespace Kaleido;

/// <summary>
/// Standard error response returned by Kaleido endpoints when a request fails.
/// Used by the exception middleware, Queryable endpoints, and the Registry endpoint.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record KaleidoErrorResponse(
    IReadOnlyList<KaleidoError> Errors);

/// <summary>
/// Standard error codes used across Kaleido endpoints.
/// </summary>
public static class KaleidoErrorCodes
{
    /// <summary>
    /// The request could not be read or bound: an invalid argument, a malformed request or a body
    /// that does not fit the expected shape. Returned with 400 Bad Request, or with the status of a
    /// malformed-request failure reported by ASP.NET Core.
    /// </summary>
    public const string ArgumentError = "argument_error";

    /// <summary>
    /// An unexpected failure with no Kaleido-specific code. Returned with 500 Internal Server Error
    /// and a generic message, without exception details.
    /// </summary>
    public const string FrameworkError = "framework_error";
}

/// <summary>
/// A single structured error with a stable machine-readable code and human-readable message.
/// </summary>
/// <param name="Code">A stable machine-readable error code (e.g. <c>"argument_error"</c>, <c>"validation_failed"</c>).</param>
/// <param name="Message">A human-readable description of the error.</param>
/// <param name="Field">The specific field that caused the error, if applicable.</param>
[ExcludeFromCodeCoverage]
public sealed record KaleidoError(
    string Code,
    string Message,
    string? Field = null);
