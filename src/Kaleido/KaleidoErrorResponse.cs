namespace Kaleido;

/// <summary>
/// Standard error response returned by Kaleido endpoints when a request fails.
/// Used by the exception middleware, Queryable endpoints, and the Registry endpoint.
/// </summary>
public sealed record KaleidoErrorResponse(
    IReadOnlyList<KaleidoError> Errors);

/// <summary>
/// Standard error codes used across Kaleido endpoints.
/// </summary>
public static class KaleidoErrorCodes
{
    public const string ArgumentError = "argument_error";
    public const string FrameworkError = "framework_error";
}

/// <summary>
/// A single structured error with a stable machine-readable code and human-readable message.
/// </summary>
/// <param name="Code">A stable machine-readable error code (e.g. <c>"argument_error"</c>, <c>"validation_failed"</c>).</param>
/// <param name="Message">A human-readable description of the error.</param>
/// <param name="Field">The specific field that caused the error, if applicable.</param>
public sealed record KaleidoError(
    string Code,
    string Message,
    string? Field = null);
